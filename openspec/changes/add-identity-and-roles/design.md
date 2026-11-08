# Design

## Context

`add-tenancy-and-isolation` dejó:
- RLS por organización: la función `public.nexgate_current_org()`, la variable de sesión `app.org_id` que fija un interceptor al abrir cada conexión, el rol `nexgate_app` sin bypass, los helpers `EnableTenantIsolation` y `GrantAppAccess`, y la guardia de catálogo.
- El middleware que lee la organización activa del claim `org_id`, más un encabezado de desarrollo que este cambio quita.

`docs/05-arquitectura-tecnica.md` sugiere OpenIddict o Keycloak, tokens con la organización activa como claim, permisos evaluados en el servidor y `GET /me/permissions`. `docs/02` define las tablas `usuario`, `membresia_organizacion` y `asignacion_rol`. Ver `proposal.md` para la motivación.

## Goals / Non-Goals

**Goals**
- Identificar al usuario de cada solicitud con un token firmado por el sistema, que diga en qué organización actúa.
- Permisos administrativos por rol y alcance, evaluados en el servidor en cada solicitud, con efecto inmediato al revocarlos.
- Que los datos de identidad respeten RLS, incluidos los que no pertenecen a una sola organización.
- Quitar el mecanismo de desarrollo sin perder la capacidad de probar con datos reales.

**Non-Goals**
- OIDC completo (código de autorización con PKCE, clientes de terceros, proveedores externos).
- Alcance por espacio, que se agrega con `add-spaces-and-access-points`.
- Acceso físico: los roles de este cambio no crean Otorgamientos ni abren puertas.
- Envío de correos: recuperación de contraseña, verificación y entrega automática de invitaciones.

## Decisions

### 1. Tokens emitidos por el propio sistema

- `POST /api/v1/auth/token` valida correo y contraseña y devuelve:
  - un **token de acceso** JWT firmado con ES256, de 15 minutos;
  - un **token de renovación** opaco: 32 bytes aleatorios en base64url.
- Claims del token de acceso: `sub` (id del usuario), `org_id` (organización activa, si hay), `sid` (id de la sesión de renovación), `iss`, `aud`, `exp` e `iat`. No lleva correo, roles ni permisos.
- La validación usa `Microsoft.AspNetCore.Authentication.JwtBearer` con 30 segundos de tolerancia de reloj.
- Clave de firma: `Identity:Tokens:SigningKey` (PEM de una clave EC P-256).
  - En `Development`, si falta, se genera una clave efímera al arrancar, con una advertencia en el log; los tokens dejan de valer al reiniciar.
  - Fuera de `Development`, si falta, la aplicación no arranca. Usa el mismo patrón de validación al inicio que tenía `TenancyOptions`.
  - El `kid` es la huella de la clave, para poder rotarla más adelante con varias claves de validación.
- **Alternativa descartada:** OpenIddict como servidor OIDC embebido. Resuelve el código de autorización y los clientes de terceros, que no se necesitan todavía. A cambio obliga a páginas de login del lado del servidor y agrega sus propias tablas globales, que habría que encajar en RLS. Se puede adoptar después sin cambiar el contrato del token (`sub`, `org_id`).
- **Alternativa descartada:** Keycloak externo. Es un servicio más para operar y su modelo de organizaciones y roles no coincide con membresías y alcances por sitio.
- **Alternativa descartada:** cookies de sesión. El panel y la PWA podrían usarlas, pero los dispositivos y los clientes móviles van a necesitar tokens portadores, y conviene un solo mecanismo.

### 2. Sesiones de renovación con rotación y detección de reutilización

- Iniciar sesión crea una **sesión** (`refresh_session`) con vencimiento absoluto de 30 días y emite su primer token (`refresh_token`, guardado solo como hash SHA-256).
- `POST /api/v1/auth/refresh` marca el token presentado como usado y emite uno nuevo en la misma sesión.
  - Si se presenta un token ya usado, se revoca la sesión entera; es la señal de que el token se filtró.
  - Puede recibir `organizationId` para cambiar la organización activa. Si no lo recibe, conserva la de la sesión.
  - Antes de emitir, verifica que la membresía siga activa.
- `POST /api/v1/auth/logout` revoca la sesión. Responde 204 aunque el token no exista, para no revelar nada.
- El hash rápido (SHA-256) es suficiente porque los tokens son aleatorios de 256 bits; no hace falta un hash lento como el de las contraseñas.
- La limpieza de sesiones vencidas queda para cuando existan los workers. Mientras tanto, las filas vencidas no conceden nada.
- **Alternativa descartada:** tokens de renovación JWT sin estado. No se pueden revocar ni detectar su reutilización.

### 3. Contraseñas

- Hash con `PasswordHasher<T>` de `Microsoft.Extensions.Identity.Core` (PBKDF2-HMAC-SHA512 con sal por usuario y formato versionado que permite subir las iteraciones y rehashear al iniciar sesión). Mínimo 12 caracteres, sin otras reglas de composición.
- Cuando el correo no tiene cuenta, se verifica igual contra un hash ficticio, para que el tiempo de respuesta no revele si la cuenta existe.
- Las pruebas bajan las iteraciones por configuración, para no tardar segundos por cada inicio de sesión.
- **Alternativa descartada:** ASP.NET Core Identity completo. Trae su propio esquema (`AspNetUsers`, `AspNetRoles` y otras) sin organizaciones ni alcances, y muchas funciones que no se usan.
- **Alternativa descartada:** Argon2 con una librería de terceros. Es mejor contra ataques con GPU, pero suma una dependencia nativa. El formato versionado deja migrar después.

### 4. Usuario actual en la sesión de base de datos

- Se agrega la función `public.nexgate_current_user()`, que devuelve `NULLIF(current_setting('app.user_id', true), '')::uuid`, y la variable `app.user_id`.
  - La crea el migrador, igual que `nexgate_current_org()`.
  - El interceptor de conexión fija las dos variables al abrir cada conexión, vacías si no hay usuario u organización.
- *Ajustado durante la implementación.* Las operaciones previas a la autenticación (aceptar una invitación creando la cuenta, emitir tokens después de validar la contraseña) no fijan variables locales a mano: crean un alcance de DI con el usuario y la organización que corresponden (`IContextScopeFactory`, decisión 8) y el interceptor los fija al abrir la conexión. Así la segunda barrera de EF (filtros y asignación de `organization_id`) también usa esos valores, y no hay dos formas de fijar el contexto.

### 5. Schema `identity`

| Tabla | Columnas principales | `organization_id` | RLS |
|---|---|---|---|
| `user_account` | id, email, normalized_email (único), password_hash, status (active, disabled), platform_operator, created_at | No: una cuenta sirve para varias organizaciones | SELECT: `id = nexgate_current_user()` o el usuario es miembro de la organización activa (subconsulta sobre `membership`). INSERT y UPDATE: `id = nexgate_current_user()`. Sin DELETE |
| `membership` | id, organization_id, user_id, created_at; único (organization_id, user_id) | Sí | `tenant_isolation` estándar, más la política permisiva `membership_own` de SELECT: `user_id = nexgate_current_user()` |
| `role_assignment` | id, organization_id, user_id, role, scope_type (organization, site), scope_id, valid_from, valid_until, created_at; FK (organization_id, user_id) a `membership` con `ON DELETE CASCADE` | Sí | `tenant_isolation` estándar |
| `invitation` | id, organization_id, normalized_email, role, scope_type, scope_id, token_hash (único), expires_at, accepted_at, accepted_by, created_by, created_at | Sí | `tenant_isolation` estándar |
| `refresh_session` | id, user_id, organization_id (la activa de la sesión, puede ser nula), created_at, expires_at, revoked_at | No: es del usuario | `user_id = nexgate_current_user()` para todas las operaciones |
| `refresh_token` | id, session_id, user_id, token_hash (único), created_at, used_at | No: es del usuario | `user_id = nexgate_current_user()` para todas las operaciones |

- Como `tenancy.organization`, las tablas sin `organization_id` tienen RLS habilitado y forzado con políticas propias, así que la guardia de catálogo las cubre sin cambios.
  - Es la lectura propuesta de la regla "todo dato de negocio lleva `organizacion_id`": cuentas y sesiones son datos de identidad de la plataforma, no datos de negocio de un tenant.
  - El proposal lo marca como punto a aprobar.
- Un administrador ve el correo de los miembros de su organización. Ve que la cuenta existe, pero no sus otras membresías, porque `membership` filtra por organización activa salvo para las propias.
- No hay FK entre schemas: `scope_id` apunta a `tenancy.site` y se valida en la aplicación con el contrato de Tenancy. Hoy no se pueden borrar sitios; cuando se pueda, el cambio correspondiente define qué pasa con las asignaciones.
- **Alternativa descartada:** `user_account` con `organization_id` y una cuenta por organización. Contradice D-04: la misma persona tendría varias contraseñas y no habría selector de organización.
- **Alternativa descartada:** `user_account` fuera de RLS, como el padrón. Un error de la aplicación podría listar todas las cuentas de la plataforma, y la regla del proyecto reserva esa excepción para People.

### 6. Búsquedas previas a la autenticación con funciones restringidas

Antes de autenticar no hay usuario ni organización, así que RLS no deja ver nada. Tres funciones `SECURITY DEFINER` resuelven esos casos:

| Función | Devuelve |
|---|---|
| `identity.find_login(normalized_email)` | id, password_hash, status y platform_operator de esa única cuenta |
| `identity.find_refresh_token(token_hash)` | token, sesión, usuario, si ya se usó y si la sesión está revocada o vencida |
| `identity.find_invitation(token_hash)` | id, organización y correo de la invitación, solo si no se aceptó y no venció |

- Son propiedad de `nexgate_owner`, fijan `search_path`, tienen `REVOKE ALL FROM PUBLIC` y solo `nexgate_app` las puede ejecutar.
- *Ajustado durante la implementación.* Con `FORCE ROW LEVEL SECURITY`, RLS aplica también al dueño, así que una función que corre como dueño no vería nada. Las tablas que leen tienen una política `owner_lookup` de solo lectura para `nexgate_owner`. El dueño ya puede todo sobre sus tablas (incluso desactivar RLS), así que no es un acceso nuevo; para `nexgate_app` nada cambia.
- Cada una busca por una clave exacta: no hay forma de listar.
- Con el resultado, la aplicación fija el usuario o la organización en la transacción y el resto del trabajo pasa por RLS normal.
- La guardia de catálogo suma un control: toda función `SECURITY DEFINER` de los schemas de negocio tiene que estar en una lista conocida y fijar `search_path`.
- **Alternativa descartada:** una marca de transacción como `app.platform_operation` que habilite leer `user_account`. Abriría la tabla entera ante cualquier error; las funciones solo devuelven una fila por clave exacta.

### 7. Resolución de la organización activa y permisos por solicitud

- **Middleware**, en el lugar del actual, después de la autenticación JWT:
  - Toma `sub` y `org_id` del token validado. El encabezado de desarrollo, `TenancyOptions` y su validador se borran.
  - Si hay `org_id`, carga en una sola consulta la membresía y las asignaciones de rol vigentes del usuario en esa organización, bajo RLS.
  - Sin membresía, la solicitud queda sin organización activa y las operaciones de negocio responden 403 `organization_context_required`.
- **Catálogo de permisos**, en código (`Identity.Contracts`):

| Rol | Permisos |
|---|---|
| administrator | `organization.read`, `sites.read`, `sites.manage`, `members.read`, `members.manage` |
| guard, resident, employee | `organization.read`, `sites.read` |

- **Alcance:**
  - Con alcance de organización, un permiso cubre todos los sitios.
  - Con alcance de sitio, cubre solo ese sitio.
  - `members.*` y la creación de sitios exigen alcance de organización.
- **`ICurrentPermissions`** (contrato en `BuildingBlocks`, implementado por Identity) responde dos preguntas: si el usuario tiene un permiso, y sobre qué sitios lo tiene (todos o una lista).
  - El filtro `RequirePermission(permission)` responde 403 `permission_denied`.
  - El servicio de sitios recibe la lista de sitios permitidos y filtra. Un sitio fuera del alcance da 404, igual que uno inexistente.
- **Alternativa descartada:** permisos dentro del token. Revocar un rol tardaría hasta 15 minutos en aplicarse.
- **Alternativa descartada:** roles y permisos configurables en base de datos. No hace falta todavía, y el catálogo en código se prueba más fácil. Se puede mover a tablas cuando aparezcan roles personalizados.
- **No es el motor de Otorgamientos.** Estos permisos son administrativos, sobre la API. El acceso físico, con su regla de que gana la denegación, sigue siendo exclusivo del motor de Otorgamientos (cambio 5).

### 8. Nombres de organización para el selector

`GET /api/v1/me` lista las membresías propias (política `membership_own`). Para el nombre de cada organización, Identity llama al contrato de Tenancy `IOrganizationDirectory.GetActiveOrganizationAsync()`, una vez por membresía, dentro de un alcance de DI propio con esa organización como activa.

- `BuildingBlocks` expone `IContextScopeFactory.Create(userId, organizationId)` (*ajustado durante la implementación*: el diseño original solo recibía la organización). Crea un alcance de DI cuyo `RequestContext` tiene ese usuario y esa organización; el interceptor los fija en las conexiones de ese alcance. Lo usan también el inicio de sesión, la aceptación de invitaciones y la invitación del administrador emitida por el operador.
- Solo se usa con organizaciones donde el usuario es miembro, verificado antes.
- **Alternativa descartada:** ampliar la política de `tenancy.organization` con una subconsulta a `identity.membership`. Acopla dos módulos en la base y deja a Tenancy dependiendo del esquema de Identity.
- **Alternativa descartada:** guardar una copia del nombre en `membership`. Queda desactualizada al renombrar hasta que exista el outbox.

### 9. Invitaciones

- El token es aleatorio de 256 bits y se guarda solo su hash. Vence a los 7 días (configurable) y se devuelve una sola vez, en la respuesta de creación.
- La respuesta tiene la misma forma exista o no la cuenta del correo.
- `POST /api/v1/invitations/accept` con `{ token, password? }`:
  1. `find_invitation` obtiene la organización. Si no aparece, responde 400 `invalid_invitation`.
  2. Busca la cuenta del correo con `find_login`.
  3. Si no existe, exige `password` y crea la cuenta con un id nuevo, fijando `app.user_id` local a la transacción.
  4. Si existe, exige un token de acceso de esa misma cuenta. Si falta, responde 401 y no consume la invitación.
  5. Con `app.org_id` local igual a la organización de la invitación, crea la membresía si no existía y la asignación de rol, y marca la invitación como aceptada. Todo va en una sola transacción.
- El rol y el alcance se validan al invitar y otra vez al aceptar, por si el sitio dejó de existir.

### 10. Regla del último administrador

- Las operaciones que pueden quitar un administrador toman primero un `pg_advisory_xact_lock` con una clave derivada de la organización. Son la revocación de una asignación y la baja de un miembro.
- Con el lock tomado, cuentan las asignaciones vigentes de administrador con alcance de organización que quedarían. Si no queda ninguna, responden 409 `last_administrator`.
- El lock evita que dos revocaciones concurrentes dejen la organización sin administrador.
- **Alternativa descartada:** transacción `SERIALIZABLE` con reintentos. Funciona, pero obliga a manejar reintentos en cada operación de membresía.

### 11. Operador de plataforma

- Se marca con la columna `user_account.platform_operator`. Se lee de la base en cada solicitud a un endpoint de plataforma, filtro `RequirePlatformOperator()`; no se lee del token.
- Comando del host: `dotnet run --project src/Nexgate.Host -- create-platform-operator --email <correo>`.
  - La contraseña se lee de `NEXGATE_OPERATOR_PASSWORD` o de la entrada estándar, nunca de los argumentos, para que no quede en el historial.
  - Si la cuenta existe, solo le agrega la marca y no toca la contraseña.
  - Corre con el rol app, fijando `app.user_id` local a la transacción.
- `POST /api/v1/platform/organizations` recibe `{ name, administratorEmail }`.
  1. Crea la organización, con la marca `app.platform_operation` como hasta ahora.
  2. Con el contrato `IAdministratorInvitations` de Identity, crea la invitación en un alcance donde esa organización está activa.
  3. Responde la organización y la invitación.
- Esas dos escrituras no son atómicas, porque son dos módulos y dos conexiones.
  - Si falla la segunda, la organización queda sin invitación.
  - Para eso existe `POST /api/v1/platform/organizations/{id}/administrator-invitations`, que también sirve cuando el token se pierde.
  - El operador no obtiene acceso a los datos de la organización: no recibe membresía.
- **Alternativa descartada:** una transacción compartida entre los `DbContext` de Tenancy e Identity. Ata los dos módulos a la misma conexión y complica extraerlos (D-27).

### 12. Endpoints

| Método y ruta | Quién | Respuesta |
|---|---|---|
| `POST /api/v1/auth/token` | anónimo | 200 tokens · 401 `invalid_credentials` · 403 `organization_not_available` · 429 |
| `POST /api/v1/auth/refresh` | anónimo con token de renovación | 200 tokens · 401 `invalid_refresh_token` · 403 `organization_not_available` |
| `POST /api/v1/auth/logout` | anónimo con token de renovación | 204 |
| `GET /api/v1/me` | autenticado | 200 |
| `GET /api/v1/me/permissions` | autenticado | 200 |
| `POST /api/v1/invitations` | `members.manage` (organización) | 201 · 400 · 403 |
| `POST /api/v1/invitations/accept` | anónimo o autenticado | 200 · 400 `invalid_invitation` · 401 · 429 |
| `GET /api/v1/members` | `members.read` (organización) | 200 |
| `DELETE /api/v1/members/{userId}` | `members.manage` (organización) | 204 · 404 · 409 `last_administrator` |
| `POST /api/v1/members/{userId}/role-assignments` | `members.manage` (organización) | 201 · 400 · 404 |
| `DELETE /api/v1/members/{userId}/role-assignments/{id}` | `members.manage` (organización) | 204 · 404 · 409 `last_administrator` |
| `POST /api/v1/platform/organizations` | operador de plataforma | 201 · 400 · 401 · 403 |
| `POST /api/v1/platform/organizations/{id}/administrator-invitations` | operador de plataforma | 201 · 400 · 403 · 404 |

- Los endpoints de Tenancy pasan a exigir permisos: `GET /organization` exige `organization.read`; `GET /sites` y `GET /sites/{id}`, `sites.read`; `POST /sites`, `sites.manage` con alcance de organización; `PUT /sites/{id}`, `sites.manage` sobre ese sitio.
- Sin token o con un token inválido en un endpoint que exige autenticación, la respuesta es 401 `authentication_required`, con el formato de errores de `platform-api`.
- **Límite de intentos:** el limitador de ASP.NET Core aplica una ventana fija por IP sobre `auth/token` e `invitations/accept`, por defecto 10 por minuto y configurable. La clave del limitador no incluye el correo.

### 13. Telemetría y logs

- Los logs y trazas llevan `user.id` (seudónimo) además de `organization.id`. Nunca llevan correo, contraseña ni tokens.
- La plantilla de logs de inicio de sesión no tiene parámetro para el correo, y una prueba captura los logs de un inicio de sesión fallido y verifica que el correo no aparece.

## Cómo se prueba

- **Unitarias:**
  - catálogo de permisos y evaluación de alcance y vigencia;
  - validación de invitaciones, asignaciones y contraseñas;
  - emisión y validación de tokens: firma, vencimiento y claims;
  - normalización del correo.
- **Integración con PostgreSQL real, conectando como `nexgate_app`:**
  - inicio de sesión con una y varias membresías, elección de organización y respuesta idéntica para credenciales inválidas, cuenta inexistente y cuenta deshabilitada;
  - renovación, rotación, reutilización con revocación de la sesión, cambio de organización y cierre de sesión;
  - invitación con correo nuevo y existente, con la misma respuesta al administrador; aceptación en todos sus casos;
  - quitar un rol o un miembro tiene efecto en la siguiente solicitud con el mismo token;
  - regla del último administrador, incluida la concurrencia (dos revocaciones en paralelo);
  - alcance de sitio en los endpoints de Tenancy: listado filtrado, 404 fuera del alcance, 403 al crear;
  - SQL crudo sin filtros sobre las tablas de identidad: cada usuario ve solo lo suyo y un administrador solo las cuentas de sus miembros; sin usuario ni organización, cero filas;
  - las funciones `SECURITY DEFINER` no permiten listar y ningún otro rol las puede ejecutar;
  - el encabezado `X-Nexgate-Organization` se ignora siempre;
  - comando `create-platform-operator`.
- **Guardia de catálogo:** cubre el schema `identity` sin cambios y suma la verificación de las funciones `SECURITY DEFINER`.
- **Arranque:** no arranca fuera de `Development` sin clave de firma.
- **Ayudantes de prueba:** `NexgateFactory` deja de usar el encabezado. Obtiene tokens por el flujo real: siembra un operador con el servicio del comando, crea la organización, acepta la invitación e inicia sesión, con iteraciones de hash bajas. Expone, por ejemplo, `CreateClientAsAsync(role, scope)`.

## Risks / Trade-offs

- **Fuga del token de renovación.** Se mitiga con rotación, detección de reutilización y vencimiento absoluto. La guarda segura en el cliente se define con el panel (cambio 7).
- **Consulta extra por solicitud para membresía y permisos.** Es una consulta indexada por (organization_id, user_id). Se mide; si pesa, se cachea por solicitud, nunca entre solicitudes.
- **La entrega del token de invitación queda en manos del administrador hasta que exista Notifications.** El token sirve una sola vez y vence. La respuesta lo devuelve solo a quien tiene `members.manage`.
- **El alta de organización y su invitación no son atómicas.** Mitigado con el endpoint de reemisión (decisión 11).
- **Lectura de la regla de `organizacion_id` para cuentas y sesiones.** Requiere aprobación explícita (ver proposal).
- **Tamaño del cambio.** Más de 10 requisitos; se divide en grupos de tareas de una sesión cada uno.

## Migration Plan

- No hay datos de usuarios previos. Las organizaciones creadas con el mecanismo de desarrollo quedan sin administrador; se recuperan con el endpoint de reemisión o recreando el volumen.
- El README reemplaza el uso del encabezado por el flujo nuevo: crear el operador, crear la organización, aceptar la invitación e iniciar sesión.
- Rollback: revertir la rama. La migración de `identity` no toca otros schemas, y la del interceptor solo agrega `app.user_id`.

## Open Questions

- ¿Se aprueba que `user_account` y las sesiones queden aisladas por usuario y no por organización (decisión 5)? Bloquea la implementación.
- ¿Duración de las sesiones de renovación? Propuesta: 30 días absolutos. Para el guardia en un puesto fijo quizá convenga menos; se revisa con el panel.
- ¿Los roles residente y empleado necesitan ver la lista de sitios? Propuesta: sí, solo lectura, para el selector del panel. Se ajusta en el catálogo si se decide lo contrario.
