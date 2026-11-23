# Proposal

## Why

El cambio 5 dejó la política compilada de cada punto de acceso y el algoritmo de decisión, pero ninguna puerta los recibe todavía. Para que un controlador ESP32 decida solo y sin red (`docs/04`), tiene que poder darse de alta en un sitio, identificarse ante el backend, descargar la política de sus puntos con una versión que avance sola, y devolver lo que pasó en la puerta. Sin eso no hay prototipo de punta a punta (fase 1 de `docs/07`) ni descuento de usos de los otorgamientos.

## What Changes

- **Módulo Devices**, con:
  - **Alta de dispositivos** por sitio desde el panel: el administrador crea el dispositivo, recibe un **código de reclamo** de un solo uso (se muestra una vez y vence) y le asigna los puntos de acceso que controla. Un punto de acceso lo controla a lo sumo un dispositivo.
  - **Reclamo** (aprovisionamiento): el dispositivo genera su par de claves, manda el código y una solicitud de certificado, y recibe un **certificado de cliente** firmado por la autoridad de certificación de dispositivos de Nexgate, la clave HMAC de su sitio (para calcular el hash de lo que lee, `docs/03`) y la clave pública con la que se firman sus políticas.
  - **Identidad por certificado de cliente (mTLS)**: después del reclamo, toda solicitud del dispositivo se autentica con su certificado. El backend reconoce el certificado vigente de cada dispositivo; revocar el dispositivo o volver a generarle un código invalida el certificado anterior en la solicitud siguiente.
  - **Política por dispositivo**: un paquete con la política v1 de cada punto que controla, una **versión monotónica** que solo avanza cuando el contenido cambia, y una **firma** que el dispositivo verifica antes de guardarla. La descarga es condicional: si el dispositivo ya tiene la versión vigente, la respuesta es "sin cambios".
  - **Latido**: el dispositivo informa su versión de firmware y de política; el backend registra el último contacto y responde la versión vigente, la hora del servidor y cada cuánto volver a consultar.
- **Módulo Events**, con:
  - **Ingesta de eventos de acceso** por lotes: cada evento trae un identificador generado por el dispositivo (idempotencia), el instante, el punto, el hash de la credencial si la conoce, la decisión, el motivo y la versión de política con la que decidió. Los eventos que el dispositivo encoló sin red llegan después con su instante original.
  - Al registrar un evento permitido, el backend **descuenta un uso** del otorgamiento que lo autorizó, cuando tiene usos limitados (lo que el cambio 5 dejó pendiente).
  - **Consulta de eventos** de un sitio para el panel, del más reciente al más antiguo, con filtros por punto, sujeto y rango de fechas.
- **Transporte**: HTTPS para todo el tráfico de dispositivos, con sondeo periódico. Es la alternativa que `docs/04` admite para el prototipo; D-30 (MQTT sobre TLS) se mantiene como destino y se agrega en un cambio posterior para avisos inmediatos y comandos.
- **Permisos** nuevos: `devices.read` (administrador y guardia), `devices.manage` (administrador) y `events.read` (administrador y guardia), con alcance de sitio entero.
- **Integridad entre módulos**: no se elimina un punto de acceso asignado a un dispositivo.

Fuera de alcance:
- MQTT: aviso inmediato de política nueva, comandos (abrir a distancia, reiniciar, sincronizar) y estado retenido. Mientras tanto, el dispositivo sondea.
- Renovación automática del certificado antes de su vencimiento, OTA y salud del dispositivo (batería, deriva del reloj, novedad por falta de contacto): `add-device-ota-and-health`.
- Despliegue detrás de un proxy que termine TLS y reenvíe el certificado: se define con la infraestructura de producción.
- Eventos que no son decisiones de acceso (puerta forzada, tamper, pulsador de salida), presencia y novedades: `add-presence-and-incidents`.
- Particionado mensual de la tabla de eventos y retención: `add-privacy-and-retention`.
- Recompilación disparada por eventos internos (outbox) y deltas de política: la política se compila al consultarla.
- Firmware y simulador de dispositivos como programa aparte: las pruebas de integración hacen de dispositivo.
- Alcance de espacio para los permisos nuevos (igual que en el cambio 5).
- Pantallas del panel.

## Capabilities

### New Capabilities

- `device-provisioning`: alta de dispositivos, código de reclamo, reclamo con certificado de cliente, autenticación mTLS, asignación de puntos, revocación y aislamiento.
- `policy-sync`: paquete de política por dispositivo, versión monotónica, firma, descarga condicional y latido.
- `event-ingestion`: ingesta idempotente de eventos de acceso, descuento de usos y consulta de eventos.

### Modified Capabilities

- `role-scopes`: el catálogo de permisos por rol suma `devices.read`, `devices.manage` y `events.read`.
- `access-points`: un punto de acceso asignado a un dispositivo no se puede eliminar.

## Impact

- Código nuevo: módulos Devices y Events en las cuatro capas; controllers del panel (`/sites/{id}/devices`, `/devices/{id}`, `/sites/{id}/access-events`) y de dispositivos (`/device/...`); migraciones de los schemas `devices` y `events`; vectores de verificación de firma en `vectors/device-policy/`.
- Código modificado: catálogo de permisos de Identity; `Service.Access.Contracts` suma la clave HMAC de un sitio, la búsqueda de una credencial por hash y el descuento de un uso; la API suma la autenticación por certificado, un endpoint HTTPS que acepta certificados de cliente y un límite de solicitudes para el reclamo; composición de módulos y reglas de arquitectura.
- Dependencias entre módulos: `Service.Devices → Service.Access.Contracts`, `Service.Spaces.Contracts` y `Service.Tenancy.Contracts`; `Service.Events → Service.Access.Contracts`, `Service.Spaces.Contracts` y `Service.Tenancy.Contracts`. Ningún módulo anterior depende de Devices ni de Events.
- Configuración: `Devices:CertificateAuthority` (certificado y clave de la autoridad de dispositivos) y `Devices:PolicySigningKey`. En `Development` hay valores fijos de desarrollo; fuera de `Development` son obligatorios y no pueden ser los de desarrollo. Endpoint de Kestrel para dispositivos con certificado de cliente.
- Contrato OpenAPI: operaciones de dispositivos, reclamo, política, latido, eventos y consulta de eventos, con el esquema de seguridad `mutualTLS` en las de dispositivo.
- **Multitenancy:** todas las tablas nuevas llevan `organization_id` con la política RLS estándar. Una solicitud de dispositivo opera solo en la organización y el sitio de ese dispositivo, tomados del certificado verificado y nunca del contenido. El reclamo busca el código entre organizaciones con una función de base de datos acotada, como la aceptación de invitaciones.
- **Seguridad:** el código de reclamo y el certificado se guardan solo como hash; el código es de un solo uso, vence y el reclamo tiene límite de solicitudes. La clave privada del dispositivo nunca sale de él. La clave HMAC del sitio solo viaja en la respuesta del reclamo. La política va firmada (la firma de los bytes exactos viaja en un encabezado) y el dispositivo rechaza una política sin firma válida.
- **Privacidad:** los eventos referencian sujeto y credencial por identificador; el hash de una credencial desconocida no se guarda. Ni el código de reclamo, ni la clave del sitio, ni hashes de credenciales se registran en logs ni trazas.
- Decisiones relacionadas: D-30 (se implementa con HTTPS como transporte del prototipo; MQTT queda para después), D-13 (descuento de usos), D-28. Agrega D-35 (identidad de dispositivos por certificado de cliente emitido por Nexgate).
