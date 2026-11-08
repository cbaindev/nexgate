# Proposal

## Why

Hoy la organización activa solo se puede fijar con un encabezado de desarrollo y cualquiera que llegue a la API puede operar sobre los sitios de esa organización. Antes de agregar espacios, puntos de acceso y permisos físicos hace falta saber **quién** hace cada solicitud, **en qué organización** actúa y **qué puede hacer** ahí. Es la base de D-03 (roles por alcance), D-04 (varias organizaciones por usuario) y D-05 (un solo panel con vistas por rol), y es lo que permite quitar el mecanismo de desarrollo que dejó `add-tenancy-and-isolation`.

## What Changes

- Crear el módulo **Identity** con cuentas de usuario de la plataforma: correo único y contraseña guardada con hash.
- **Inicio de sesión** con correo y contraseña. Devuelve un token de acceso de vida corta que lleva la organización activa y un token de renovación que rota en cada uso. Incluye renovación, cambio de organización activa y cierre de sesión (D-04).
  - Con una sola membresía la organización queda elegida sola. Con varias, el cliente elige; sin elegir, no hay organización activa y las operaciones de negocio responden 403, como hoy.
  - Credenciales inválidas, cuenta inexistente y cuenta deshabilitada responden igual. Los intentos se limitan por tasa.
- **Membresías e invitaciones**: un administrador invita a alguien por correo con un rol y un alcance. La invitación se acepta creando la cuenta, o iniciando sesión si la cuenta ya existe. La respuesta al administrador es la misma en ambos casos, para no revelar si esa persona ya usa la plataforma con otra organización.
- **Roles por alcance** (D-03, D-05): los roles son administrador, guardia, residente y empleado. El alcance es la organización o un sitio, con vigencia opcional. Cada rol da un conjunto fijo de permisos administrativos y los permisos se evalúan en el servidor en cada solicitud.
  - Endpoint `GET /api/v1/me/permissions` para que la interfaz muestre solo lo permitido (D-05).
  - No se puede quitar el último administrador de la organización.
- **Operador de plataforma**: crea organizaciones, y cada organización nace con la invitación de su primer administrador. El primer operador se crea con un comando del host, sin endpoint público.
- **Endpoints de Tenancy protegidos por permisos**: un rol con alcance de sitio ve y modifica solo ese sitio, y crear sitios exige alcance de organización.
- **Retirar el mecanismo de desarrollo** (encabezado `X-Nexgate-Organization` y opción `Tenancy:DevelopmentAccess:Enabled`). La organización activa sale solo del token.

Fuera de alcance:
- **Alcance por espacio** (D-03): no hay espacios todavía. Lo agrega `add-spaces-and-access-points`, que extiende `role-scopes`.
- **Acceso físico**: los roles de este cambio dan permisos sobre la API, no abren puertas. Todo permiso físico sigue siendo un Otorgamiento (cambios 5 y 9 en adelante).
- Recuperación de contraseña, verificación de correo y segundo factor: requieren envío de mensajes (módulo Notifications). Mientras tanto, el token de la invitación se le devuelve al administrador, que lo hace llegar por su cuenta.
- Inicio de sesión con proveedores externos (Google, Microsoft) y OIDC completo con código de autorización.
- Delegación entre organizaciones (D-04), deshabilitar cuentas desde la plataforma, suspensión de organizaciones y auditoría de cambios de rol.
- Vínculo entre la cuenta de usuario y el Sujeto o la Persona (`sujeto_ref` en `docs/02`): llega con `add-people-registry`.
- Autenticación de dispositivos: llega con `add-device-sync-protocol`.

## Capabilities

### New Capabilities

- `authentication`: cuentas de usuario, inicio de sesión, renovación, cambio de organización activa y cierre de sesión.
- `organization-membership`: invitaciones, aceptación, listado de miembros y baja de miembros de la organización activa.
- `role-scopes`: roles con alcance de organización o sitio, vigencia, permisos efectivos y la regla del último administrador.

### Modified Capabilities

- `tenant-isolation`: la organización activa sale solo del token del usuario autenticado y además tiene que existir una membresía activa en ella. Desaparece el mecanismo de desarrollo y se agrega el aislamiento de los datos de identidad que no pertenecen a una sola organización.
- `organizations-and-sites`: crear organizaciones exige un operador de plataforma y crea la invitación del primer administrador. Las operaciones sobre sitios exigen permisos y respetan el alcance.

## Impact

- **Código nuevo:** módulo `Identity` (Contracts, Domain, Application, Infrastructure). En `BuildingBlocks`, el usuario actual, su fijación en la sesión de base de datos y el filtro de permisos para endpoints. Comando del host `create-platform-operator`.
- **Código modificado:** se reemplaza la resolución de organización y se borra el mecanismo de desarrollo. Los endpoints de Tenancy pasan a exigir permisos. Las pruebas usan tokens reales en vez del encabezado de desarrollo.
- **Base de datos:** schema `identity` con las tablas `user_account`, `membership`, `role_assignment`, `invitation` y `refresh_session`, y funciones de búsqueda previas a la autenticación (ver design).
- **Dependencias:** validación de JWT de ASP.NET Core y el hash de contraseñas de `Microsoft.Extensions.Identity.Core`, sin el resto de ASP.NET Core Identity.
- **Configuración:** clave de firma de tokens (obligatoria fuera de `Development`) y duración de los tokens. Se borra `Tenancy:DevelopmentAccess`.
- **Multitenancy:**
  - `membership`, `role_assignment` e `invitation` llevan `organization_id` con la política RLS estándar. `membership` además le deja a cada usuario ver sus propias membresías, para elegir organización.
  - **Requiere tu aprobación:** `user_account` y `refresh_session` no pueden pertenecer a una sola organización, porque una cuenta sirve para varias (D-04). Son datos de identidad de la plataforma, no datos de negocio de un tenant, como ya pasa con `tenancy.organization`.
    - Igual tienen RLS habilitado y forzado, pero aislado por usuario y no por organización: cada usuario ve su propia cuenta y un administrador ve las cuentas de los miembros de su organización.
    - Esto se lee como compatible con la regla "todo dato de negocio lleva `organizacion_id`", pero no es la excepción del padrón. Si preferís otra lectura, el diseño cambia antes de implementar.
  - El nombre de las organizaciones del selector se lee con la organización correspondiente activa, una por una. No hace falta abrir políticas de Tenancy a otras organizaciones.
- **Seguridad:**
  - **Contraseñas:** se guardan con hash PBKDF2 y sal por usuario.
  - **Tokens:**
    - El de acceso dura 15 minutos y va firmado con clave asimétrica.
    - El de renovación se guarda solo como hash. Rota en cada uso y, si se reutiliza uno ya rotado, se revoca toda la sesión.
    - Fuera de `Development` la aplicación no arranca sin la clave de firma.
  - **Permisos:** se leen de la base en cada solicitud, así que quitar un rol o una membresía tiene efecto inmediato aunque el token siga vigente.
  - **Ataques al inicio de sesión:** se limitan los intentos por tasa. Una cuenta inexistente responde igual que una contraseña equivocada.
- **Privacidad:**
  - **Logs:** el correo es dato personal y nunca va a logs ni trazas. Los logs llevan solo identificadores, y el del usuario es seudónimo.
  - **Otras organizaciones:** un administrador ve el correo de los miembros de su organización y nada sobre sus membresías en otras.
  - **Invitaciones:** la respuesta no revela si el correo ya tiene cuenta (en línea con D-22).
- **Tamaño:** el cambio tiene más de 10 requisitos y la CLI lo va a advertir. Se mantiene junto porque inicio de sesión, membresías y roles no sirven por separado: sin invitaciones no hay forma de sumar usuarios, y sin roles no se puede quitar el mecanismo de desarrollo. Se estima en tres sesiones de trabajo.
- **Decisiones relacionadas:** D-03, D-04, D-05; completa D-02 y D-28; prepara D-22 (vínculo con Sujeto) y D-24.
