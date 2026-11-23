# Tasks

## 1. Dispositivos en el panel (módulo Devices)

- [x] 1.1 Crear `Nexgate.Domain.Devices`, `Nexgate.Service.Devices(.Contracts)` y `Nexgate.Data.Devices`, agregarlos a la solución y a las reglas de arquitectura; verificar con `dotnet build` y las pruebas de arquitectura
- [x] 1.2 Permisos `devices.read`, `devices.manage` y `events.read` en `Permissions` y en el catálogo de Identity; verificar con las pruebas de permisos por rol actualizadas
- [x] 1.3 Entidad `Device`, código de reclamo (generación, normalización, hash y vencimiento), tablas `device` y `device_access_point` con RLS; verificar con pruebas unitarias del código, `migrate` y `has-pending-model-changes`
- [x] 1.4 `DevicesController`: alta (con código una sola vez), listado, detalle, modificación, eliminación, asignación de puntos, nuevo código y revocación; Devices implementa `IAccessPointUsage`; verificar con los escenarios de alta, listado, asignación, permisos y alcance de `device-provisioning` y el de eliminación bloqueada de `access-points`

## 2. Reclamo e identidad por certificado

- [x] 2.1 Configuración `Devices:CertificateAuthority` y `Devices:PolicySigningKey` (valores de desarrollo y validación de arranque fuera de `Development`); verificar con pruebas de arranque
- [x] 2.2 Emisión de certificados desde un CSR P-256 en `Domain.Devices`; verificar con pruebas unitarias (CSR válido, otra curva, firma inválida, sujeto ignorado, `clientAuth`)
- [x] 2.3 `ISiteCredentialKeys` en `Access.Contracts`, función `devices.find_claim` y `POST /device/claim` con límite de solicitudes; verificar con los escenarios de `Device claim`
- [x] 2.4 Esquema `DeviceCertificate`, función `devices.find_device`, contexto de organización desde el certificado, política `Device` y endpoint de Kestrel para dispositivos; filtro de prueba que inyecta el certificado; verificar con los escenarios de `Device authentication`
- [x] 2.5 Prueba con Kestrel real y TLS mutuo (reclamo y descarga de política); verificar con `dotnet test`

## 3. Política por dispositivo y latido

- [x] 3.1 Paquete v1: huella del contenido, firma ECDSA P-256 y JSON canónico en `Domain.Devices`; verificar con pruebas unitarias
- [x] 3.2 Tabla `device_policy`, versión monotónica con condición sobre la versión anterior, `GET /device/policy` con `ETag` y 304; verificar con los escenarios de `policy-sync` (incluida la versión tras un nuevo reclamo)
- [x] 3.3 `POST /device/heartbeat` y último contacto en toda solicitud de dispositivo; verificar con los escenarios de `Device heartbeat` y de funcionamiento sin conexión
- [x] 3.4 Vectores `vectors/device-policy/v1` con su `README.md` y la prueba que los verifica; verificar que el caso alterado y el de otra clave fallan

## 4. Eventos (módulo Events)

- [x] 4.1 Crear `Nexgate.Domain.Events`, `Nexgate.Service.Events(.Contracts)` y `Nexgate.Data.Events`, tabla `access_event` con RLS e índices; verificar con `dotnet build`, arquitectura, `migrate` y `has-pending-model-changes`
- [x] 4.2 `ICredentialLookup` en `Access.Contracts` y `POST /device/events` (validación del lote, idempotencia, eventos sin red); verificar con los escenarios de ingesta de `event-ingestion`
- [x] 4.3 `IGrantUses` en `Access.Contracts`, descuento con `uses_settled` y reintento en el reenvío; verificar con los escenarios de `Use consumption`, incluida la nueva versión de política
- [x] 4.4 `GET /access-events` con filtros y cursor; verificar con los escenarios de `Access event query`

## 5. Aislamiento, privacidad, contrato y documentación

- [x] 5.1 Pruebas de aislamiento con SQL directo sobre `devices.device`, `devices.device_access_point`, `devices.device_policy` y `events.access_event`; verificar con `dotnet test`
- [x] 5.2 Prueba de logs: ni el código de reclamo, ni la clave del sitio, ni el certificado, ni hashes de credenciales aparecen en los logs de alta, reclamo, política y eventos; verificar con `dotnet test`
- [x] 5.3 Regenerar `api/nexgate-v1.openapi.json` (con el esquema `mutualTLS` en las operaciones de dispositivo) y revisar el diff; verificar con las pruebas de contrato
- [x] 5.4 Actualizar `docs/01` (D-35 y nota en D-30), `docs/02` (tablas de Devices y Events), `docs/03` (descuento de usos eventual), `docs/04` (comunicación HTTPS, reclamo con certificado, credenciales con HMAC por sitio), `docs/05` (módulos y endpoint de dispositivos) y `docs/08` (cambio 6 implementado); verificar releyendo contra los specs
- [x] 5.5 Build limpio y todas las pruebas; verificar `dotnet build` sin warnings y `dotnet test` en verde

## Notas de implementación

- La firma del paquete cubre los **bytes exactos** del cuerpo y viaja en el encabezado `X-Nexgate-Signature`, en lugar de un campo `signature` sobre el JSON canónico: el firmware verifica y guarda lo que recibió sin reproducir la serialización del backend. Se ajustaron el spec `policy-sync` y la decisión 4 del design.
- Events no depende de `Devices.Contracts`: la API le pasa el dispositivo y el sitio tomados del certificado. La consulta quedó en `GET /sites/{siteId}/access-events`, como el resto de los listados por sitio.
- `IDeviceAuthenticator` recibe el certificado en DER y calcula la huella en el servicio, así la API no usa el dominio de Devices.
- En las pruebas, el filtro que inyecta el certificado también marca la solicitud como HTTPS: el esquema de certificado ignora las solicitudes HTTP, como haría Kestrel sin TLS.
- La prueba con Kestrel real corre fuera de `Development`, porque `appsettings.Development.json` declara los endpoints de desarrollo (HTTP 5095 y HTTPS de dispositivos 7443, con el certificado de desarrollo de .NET).
- La decisión y el motivo de cada evento los valida el servicio junto con el resto del lote (no un atributo del modelo), así el 400 nombra todos los errores del lote.
- La prueba de contrato acepta respuestas 304 sin cuerpo.
- Verificación final: build sin warnings, todas las pruebas en verde (46 de arquitectura y 400 de la API, con los 6 vectores de firma), `migrate` aplicado, `has-pending-model-changes` sin cambios en Devices y Events, y el contrato OpenAPI solo agrega operaciones, esquemas y el esquema de seguridad `DeviceCertificate`.
