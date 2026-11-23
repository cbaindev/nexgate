# Design

## Context

El cambio 5 dejó en Access la política v1 de cada punto de acceso (`IAccessPolicyService.CompileAsync`), su hash canónico, el algoritmo de decisión y la clave HMAC por sitio derivada de `Access:CredentialKey`. `docs/04` describe el dispositivo (ESP32, decide sin red, guarda política y cola de eventos en flash), los tópicos MQTT propuestos y el aprovisionamiento con un código en la caja. La infraestructura local ya tiene un Mosquitto sin TLS, sin usar. Ver `proposal.md` para el alcance y las dos decisiones tomadas con el usuario: **HTTPS ahora y MQTT después**, e **identidad por certificado de cliente (mTLS)**.

## Goals / Non-Goals

**Goals**
- Un dispositivo puede pasar de "en la caja" a "decidiendo con su política" y devolver sus eventos, todo probado de punta a punta con la API y PostgreSQL.
- Identidad fuerte por dispositivo: la clave privada nunca sale de él y el backend puede invalidarla al instante.
- Versión de política monotónica sin depender todavía de eventos internos (outbox).

**Non-Goals**
- MQTT, comandos, renovación de certificados, OTA, salud, particionado y retención de eventos.

## Decisions

### 1. Transporte HTTPS con sondeo

Endpoints de dispositivo bajo `api/v1/device`:

| Operación | Ruta | Autenticación |
|---|---|---|
| Reclamo | `POST /device/claim` | ninguna (código de reclamo, con límite de solicitudes) |
| Política | `GET /device/policy` | certificado; `If-None-Match` con la versión |
| Latido | `POST /device/heartbeat` | certificado |
| Eventos | `POST /device/events` | certificado |

- El dispositivo late cada `pollSeconds` (60 por defecto, configurable en `Devices:PollSeconds`) y descarga la política cuando la versión que le responden es distinta de la suya.
- **Alternativa descartada:** MQTT ya (decisión del usuario). Suma broker con TLS y autenticación por dispositivo, un puente en Workers y pruebas con broker. Se agrega después para avisos inmediatos y comandos; la descarga del paquete sigue siendo por HTTPS como dice `docs/04`.
- **Alternativa descartada:** long-polling. Ata conexiones abiertas por dispositivo para ganar segundos que el prototipo no necesita.

### 2. Autoridad de certificación de dispositivos y reclamo

- **Autoridad propia** (`Devices:CertificateAuthority:Certificate` y `:Key`, PEM, ECDSA P-256). En `Development` hay una autoridad fija de desarrollo en código (como la clave de credenciales); fuera de `Development` el arranque falla si falta o si es la de desarrollo. Las pruebas generan una por corrida.
- **Código de reclamo**: 10 caracteres de un alfabeto sin ambiguos (`23456789ABCDEFGHJKMNPQRSTUVWXYZ`, unos 49 bits), mostrado agrupado (`ABCDE-23456`) y aceptado sin guiones ni mayúsculas. Se guarda su SHA-256, vence a los 7 días y es de un solo uso. Límite de solicitudes por IP en el reclamo con la misma infraestructura que el inicio de sesión.
- **Búsqueda entre organizaciones**: el reclamo no tiene organización activa. Igual que `identity.find_invitation`, una función `devices.find_claim(hash)` con `SECURITY DEFINER` devuelve solo `(device_id, organization_id)` de un dispositivo pendiente con ese código vigente; a partir de ahí la solicitud corre con esa organización activa y RLS normal.
- **Solicitud de certificado (CSR)**: PKCS#10 en PEM, clave P-256, firma válida. El sujeto del CSR se ignora: el certificado emitido tiene sujeto `CN=<deviceId>`, uso extendido `clientAuth`, número de serie aleatorio y vigencia de 3 años (`Devices:CertificateValidityDays`). Se guardan el SHA-256 del certificado (huella) y su vencimiento.
- **Respuesta del reclamo**: `deviceId`, `siteId`, certificado y certificado de la autoridad (PEM), `credentialKey` (clave HMAC del sitio, base64), `policyKey` (clave pública de firma, PEM) y `pollSeconds`. Es la única respuesta que lleva la clave del sitio; no se registra.
- `Service.Access.Contracts` suma `ISiteCredentialKeys.GetSiteKey(siteId)` para no duplicar la derivación.
- **Alternativa descartada:** token secreto por dispositivo (decisión del usuario). Más simple, pero el secreto viaja y se puede copiar; con mTLS la clave privada nunca sale del dispositivo (y queda en flash cifrada, `docs/04`).
- **Alternativa descartada:** revocación por CRL u OCSP. El único verificador es el backend, que compara la huella con la del dispositivo en cada solicitud: revocar es cambiar una fila.

### 3. Autenticación por certificado en la API

- Kestrel suma un endpoint HTTPS para dispositivos (`Kestrel:Endpoints:Devices`, puerto propio, `ClientCertificateMode.AllowCertificate`) y acepta cualquier certificado de cliente a nivel TLS (`AllowAnyClientCertificate`): la validación real la hace el esquema de autenticación, no el almacén del sistema operativo. El endpoint principal no pide certificados (la SPA no ve diálogos de certificado).
- Esquema `DeviceCertificate` (paquete `Microsoft.AspNetCore.Authentication.Certificate`): cadena contra la autoridad de dispositivos como única raíz (`CustomRootTrust`), sin revisión de revocación, uso `clientAuth`. En `OnCertificateValidated` se busca el dispositivo por huella con `devices.find_device(huella)` (`SECURITY DEFINER`, devuelve dispositivo, organización y sitio solo si está activo y la huella es la vigente). Las claims de la identidad son `device_id`, `org` y `site_id`.
- `RequestContextMiddleware` completa la organización activa desde la claim `org` de una identidad de dispositivo, sin permisos de usuario. Los controllers de dispositivo exigen la política `Device` (esquema `DeviceCertificate`); los del panel siguen con el esquema de usuario por defecto, así que una identidad de dispositivo no pasa por ellos y viceversa.
- **Pruebas**: `TestServer` no hace TLS. Las pruebas de integración inyectan el certificado en `HttpContext.Connection.ClientCertificate` con un `IStartupFilter` de prueba, el mismo lugar donde lo deja Kestrel, así el esquema real corre entero. Una prueba aparte levanta Kestrel real en un puerto local con TLS y un `HttpClientHandler` con certificado de cliente, para cubrir el camino completo.
- **Alternativa descartada:** validar en Kestrel contra el almacén del sistema. Obliga a instalar la autoridad en cada servidor y no permite distinguir el certificado vigente de uno anterior.

### 4. Paquete de política por dispositivo, versión y firma

```json
{
  "format": 1, "deviceId": "…", "siteId": "…", "version": 7, "issuedAt": "2026-11-22T13:00:00+00:00",
  "points": [ { "format": 1, "accessPointId": "…", "…": "política v1 de policy-compilation, con su hash" } ]
}
```

Encabezados: `ETag: "7"` y `X-Nexgate-Signature: <ECDSA P-256 SHA-256, IEEE P1363, base64url>`.

- **Contenido** = `points` (ordenados por `accessPointId`). Su huella es el SHA-256 del JSON canónico de `points` (mismas reglas que el hash de la política v1).
- **Versión**: `devices.device_policy` guarda la última `(version, content_hash, issued_at)` del dispositivo. Al pedir la política o latir, se compilan los puntos; si la huella difiere de la guardada, se actualiza a `version + 1` con una condición sobre la versión anterior (dos solicitudes simultáneas no saltean ni repiten números). `issuedAt` es el de esa versión, así el mismo contenido se sirve igual.
- **Firma** sobre los **bytes exactos** del cuerpo, con `Devices:PolicySigningKey` (PEM, ECDSA P-256), en el encabezado `X-Nexgate-Signature`. El dispositivo verifica y guarda esos bytes sin volver a serializarlos: no tiene que reproducir el JSON canónico del backend. `issuedAt` se guarda en segundos enteros, así el mismo contenido se sirve con los mismos bytes. Mismo criterio de configuración que la autoridad. Se firma con ECDSA P-256 porque el ESP32 lo verifica con mbedTLS y .NET lo trae sin paquetes extra.
- **Descarga condicional**: `ETag: "7"`; con `If-None-Match: "7"` y versión vigente 7 responde 304.
- La tabla de desplazamientos de la política v1 cambia cuando pasa una transición de horario: eso cuenta como cambio de contenido y es correcto.
- Compilar en cada latido cuesta una compilación por punto por minuto por dispositivo; alcanza para el prototipo. El outbox con recompilación por eventos y una caché llegan después sin cambiar el protocolo.
- **Alternativa descartada:** firmar con la clave de la autoridad. Mezcla propósitos: comprometer una clave daría las dos capacidades.
- **Alternativa descartada:** paquete con credenciales compartidas entre puntos. Ahorra espacio, pero obliga a otro formato distinto del de los vectores v1; se revisa si el tamaño molesta en el firmware.
- **Alternativa descartada:** versión por punto. El dispositivo tiene una sola política vigente; una versión por dispositivo es lo que muestra el panel y lo que informa el latido.

### 5. Tablas del schema `devices`

| Tabla | Columnas | RLS |
|---|---|---|
| `devices.device` | id, organization_id, site_id, name (único en el sitio con `lower(name)`), status (pending, active, revoked), claim_code_hash, claim_expires_at, certificate_thumbprint (único), certificate_expires_at, firmware_version, reported_policy_version, last_seen_at, created_by, created_at, updated_at | estándar por `organization_id` |
| `devices.device_access_point` | organization_id, device_id (FK cascada), access_point_id (único: un punto, un dispositivo) | estándar |
| `devices.device_policy` | device_id (PK, FK cascada), organization_id, version, content_hash, issued_at | estándar |

- Las funciones `find_claim` y `find_device` son `SECURITY DEFINER` del dueño del schema, con `search_path` fijo, y devuelven lo mínimo.
- Volver a generar el código: estado pending, huella nula, nuevo hash de código. La fila de `device_policy` se mantiene, así la versión nunca retrocede.
- Devices implementa `IAccessPointUsage`: un punto asignado no se elimina.
- **Alternativa descartada:** columna `device_id` en `spaces.access_point`. Haría depender a Spaces de Devices.

### 6. Eventos de acceso (schema `events`)

`events.access_event` (id, organization_id, site_id, device_id, device_event_id, access_point_id, occurred_at, received_at, decision, reason, credential_id, subject_id, policy_version, uses_settled). Índice único `(device_id, device_event_id)`; índice `(site_id, occurred_at desc, id)` para la consulta. RLS estándar.

- La ingesta inserta con `ON CONFLICT DO NOTHING` y cuenta duplicados.
- `site_id` sale del certificado; el punto se valida con `ISpaceDirectory` contra ese sitio. Se valida el lote entero antes de guardar.
- El hash se resuelve con `ICredentialLookup.FindByHashAsync(siteId, hash)` (nuevo en `Access.Contracts`), que devuelve credencial y sujeto aunque la credencial esté vencida. Un hash desconocido no se guarda.
- Sin `device_id` como clave foránea hacia Devices (otro schema): un dispositivo eliminado deja sus eventos con su identificador histórico.
- **Consulta** en `GET /sites/{siteId}/access-events` con filtros por punto, sujeto y rango `[from, to)`, y cursor opaco (`occurred_at`, `id`) en base64url; página por defecto 50, máximo 100.
- Particionado mensual pospuesto hasta `add-privacy-and-retention`, que define la retención; convertir la tabla entonces es una migración acotada.

### 7. Descuento de usos

- `IGrantUses.ConsumeAsync(siteId, accessPointId, credentialId, occurredAt)` en `Access.Contracts`. Access busca los otorgamientos `allow` del sujeto de esa credencial que cubren el punto y aplicaban en ese instante (vigencia y horario, en la zona del sitio). Si alguno no tiene límite, no descuenta. Si no, descuenta uno del que vence antes (sin vencimiento al final; desempate por creación) con `remaining_uses = remaining_uses - 1 WHERE remaining_uses > 0`.
- Los módulos tienen contextos separados, así que no hay una transacción común: el evento se inserta con `uses_settled = false`, se descuenta y se marca `true`. Si una solicitud falla en el medio, el reenvío del lote encuentra el duplicado con `uses_settled = false` y reintenta el descuento. Así nunca se descuenta dos veces por el mismo evento.
- Dos dispositivos sin red pueden permitir más pasadas que usos (cada uno decidió con su política): el límite es eventual entre dispositivos y nunca baja de cero. Queda documentado en `docs/03`.
- **Alternativa descartada:** que la entrada de la política lleve el identificador del otorgamiento. Cambia el formato v1 y expone identificadores internos al dispositivo sin necesidad.

### 8. Permisos

`devices.read` (administrador y guardia), `devices.manage` (administrador) y `events.read` (administrador y guardia), con alcance de sitio entero (`CoversSite`), igual que Access. Un usuario con alcance de espacio recibe 404 en recursos de su sitio.

### 9. Módulos y dependencias

- `Service.Devices` → `Access.Contracts` (compilar, clave del sitio), `Spaces.Contracts` (validar puntos, implementar `IAccessPointUsage`), `Tenancy.Contracts` (sitio).
- `Service.Events` → `Access.Contracts` (credencial por hash, descuento), `Spaces.Contracts` (punto) y `Tenancy.Contracts` (sitio). La API le pasa el dispositivo y el sitio tomados del certificado, así Events no depende de Devices.
- La firma, el CSR y la emisión de certificados viven en `Domain.Devices` con BCL (`System.Security.Cryptography`), sin paquetes.

### 10. Vectores de firma

`vectors/device-policy/v1/`: cada caso trae una clave pública, los bytes exactos de un paquete (base64), una firma y el resultado esperado de verificar (válido, campo alterado, firma de otra clave, firma truncada). Una prueba los verifica en el backend; el firmware usa los mismos. Se generan con una clave fija solo de vectores, que no es la de desarrollo.

## Cómo se prueba

- **Unitarias**: código de reclamo (alfabeto, normalización), emisión desde CSR (rechazos: otra curva, firma inválida), huella y firma del paquete con JSON canónico, elección del otorgamiento a descontar.
- **Integración con PostgreSQL real** (`NexgateFactory`): ABM de dispositivos, asignación, reclamo, autenticación (sin certificado, de otra autoridad, revocado, reemplazado, token de usuario en endpoint de dispositivo y al revés), política con versión y 304, latido, eventos (idempotencia, lote inválido, offline, descuento de usos y nueva versión), consulta con cursor, permisos y alcance, aislamiento con SQL directo en las tablas nuevas, logs sin secretos.
- **Kestrel real** con TLS mutuo para un reclamo seguido de una descarga de política.
- **Vectores** de firma compartidos con el firmware.

## Risks / Trade-offs

- **Sondeo**: un cambio tarda hasta `pollSeconds` en llegar a la puerta. Aceptable para el prototipo; MQTT lo resuelve.
- **Clave del sitio en cada dispositivo**: un dispositivo robado y descifrado expone la clave HMAC del sitio (ya asumido en el cambio 5). Mitigación: flash cifrada y revocación; la rotación de claves queda pendiente.
- **Certificados de 3 años sin renovación automática**: la renovación llega con `add-device-ota-and-health`, mucho antes de que venzan los primeros.
- **Compilación al consultar**: costo lineal con dispositivos y puntos; se mide en el piloto.
- **Endpoint de dispositivos en el mismo proceso**: un pico de dispositivos afecta al panel. `docs/05` ya prevé separar procesos.

## Migration Plan

Dos migraciones nuevas (`devices` y `events`), sin tocar tablas existentes. Para desplegar fuera de `Development` hay que configurar la autoridad, la clave de firma y el endpoint de Kestrel para dispositivos.

## Open Questions

- Nombre y puerto definitivos del host de dispositivos en producción, y si se termina TLS en Kestrel o en un proxy (fuera de alcance).
