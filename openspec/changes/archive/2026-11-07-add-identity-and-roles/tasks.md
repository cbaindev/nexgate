# Tasks

## 1. Schema `identity` y usuario actual en la base

- [x] 1.1 Agregar `public.nexgate_current_user()` al bootstrap del migrador y extender el interceptor para que fije `app.user_id` además de `app.org_id` (vacío sin usuario); verificar con un test de integración con pool de tamaño 1 (el usuario no pasa a la solicitud siguiente)
- [x] 1.2 Crear el módulo `Identity` (Contracts, Domain, Application, Infrastructure) con las entidades `UserAccount`, `Membership`, `RoleAssignment`, `Invitation`, `RefreshSession` y `RefreshToken` y sus validaciones (correo normalizado, contraseña de al menos 12 caracteres, rol, alcance, vigencia); verificar con tests unitarios del dominio
- [x] 1.3 Migración del schema `identity` con las tablas y políticas RLS de la decisión 5 del design, las funciones `SECURITY DEFINER` de la decisión 6 y `GrantAppAccess("identity")`; verificar aplicando `migrate` dos veces y con la guardia de catálogo en verde
- [x] 1.4 Extender la guardia de catálogo para exigir que toda función `SECURITY DEFINER` de los schemas de negocio esté en la lista conocida y fije `search_path`; verificar con `dotnet test` y comprobar que una función de prueba sin `search_path` la hace fallar
- [x] 1.5 Tests de aislamiento con SQL crudo como `nexgate_app`: cada usuario ve solo su cuenta y sesiones, un administrador ve solo las cuentas de los miembros de su organización, `membership_own` deja ver las membresías propias de otras organizaciones y nada más, sin usuario ni organización hay cero filas, y las funciones no permiten listar ni las ejecuta otro rol; verificar con `dotnet test`

## 2. Inicio de sesión y tokens

- [x] 2.1 Hash de contraseñas con `PasswordHasher<T>` (iteraciones configurables y verificación contra un hash ficticio cuando no hay cuenta); verificar con tests unitarios
- [x] 2.2 Emisión y validación de JWT ES256 (`sub`, `org_id`, `sid`, 15 minutos), opciones `Identity:Tokens` con clave efímera en `Development` y bloqueo de arranque sin clave fuera de `Development`; verificar con tests unitarios de firma, vencimiento y claims, y con un test de arranque
- [x] 2.3 `POST /api/v1/auth/token` con elección de organización, `organization_not_available` y respuesta idéntica para credenciales inválidas, cuenta inexistente y cuenta deshabilitada; verificar con tests de integración de los escenarios de `Sign-in`
- [x] 2.4 `POST /api/v1/auth/refresh` (rotación, reutilización que revoca la sesión, cambio de organización, membresía terminada) y `POST /api/v1/auth/logout`; verificar con tests de integración de `Token refresh and organization switch` y `Sign-out`
- [x] 2.5 Limitador por IP sobre `auth/token` e `invitations/accept` (429) y respuesta 401 `authentication_required` en endpoints autenticados; verificar con tests de integración
- [x] 2.6 Telemetría con `user.id` y test que captura los logs de un inicio de sesión fallido y verifica que no contienen el correo ni la contraseña; verificar con `dotnet test`

## 3. Organización activa, permisos y operador de plataforma

- [x] 3.1 Reemplazar la resolución de organización: leer `sub` y `org_id` del token, verificar la membresía y cargar las asignaciones vigentes en una sola consulta; borrar el encabezado `X-Nexgate-Organization`, `TenancyOptions` y su validador; verificar con tests de los escenarios de `Active organization resolution` (el encabezado se ignora siempre)
- [x] 3.2 Catálogo de permisos en `Identity.Contracts`, `ICurrentPermissions`, filtros `RequirePermission` y `RequirePlatformOperator`, y `GET /api/v1/me/permissions`; verificar con tests unitarios de alcance y vigencia y de integración de `Effective permissions` y `Permissions query`
- [x] 3.3 `IOrganizationScopeFactory` y `GET /api/v1/me` con los nombres de las organizaciones leídos por organización; verificar con tests de integración de `Current user` (no aparecen organizaciones ajenas)
- [x] 3.4 Proteger los endpoints de Tenancy con permisos y filtrar sitios por alcance; verificar con tests de integración de `Site operations authorized by role scope`
- [x] 3.5 Comando `create-platform-operator` (contraseña por variable de entorno o entrada estándar) y endpoints de plataforma con `administratorEmail` y reemisión de la invitación de administrador; verificar con tests de integración de `Organization creation` y `Platform operator`

## 4. Membresías, invitaciones y roles

- [x] 4.1 `POST /api/v1/invitations` con respuesta idéntica para correo nuevo y existente; verificar con tests de integración de `Invitations` comparando estado y cuerpo, salvo id, token, vencimiento y correlación
- [x] 4.2 `POST /api/v1/invitations/accept` en una sola transacción (cuenta nueva, cuenta existente autenticada, cuenta existente sin autenticar, token inválido, vencido o usado, ya miembro); verificar con tests de integración de `Invitation acceptance`
- [x] 4.3 `GET /api/v1/members` y `DELETE /api/v1/members/{userId}`; verificar con tests de integración de `Member listing` y `Member removal`, incluida la baja con efecto inmediato sobre un token vigente
- [x] 4.4 Alta y revocación de asignaciones de rol con validación del alcance por el contrato de Tenancy y regla del último administrador con lock por organización; verificar con tests de integración de `Roles and scopes` y `Last administrator protection`, incluida una prueba de dos revocaciones concurrentes

## 5. Pruebas, documentación y cierre

- [x] 5.1 Reemplazar en `NexgateFactory` el encabezado de desarrollo por ayudantes que obtienen tokens con el flujo real (operador, organización, invitación, inicio de sesión) con iteraciones de hash bajas, y migrar las pruebas existentes; verificar con `dotnet test`
- [x] 5.2 Pruebas de arquitectura: Identity solo se referencia por `Identity.Contracts` y Tenancy por `Tenancy.Contracts`; verificar con `dotnet test`
- [x] 5.3 Actualizar `README.md` (operador, organización, invitación, inicio de sesión, clave de firma) y `docs/05-arquitectura-tecnica.md` (tokens propios en lugar de OpenIddict, usuario actual en la sesión de base, funciones restringidas, permisos por solicitud); verificar siguiendo el README desde cero
- [x] 5.4 Ejecutar build limpio y todos los tests; verificar `dotnet build` y `dotnet test` verdes

## Notas de implementación

- `IOrganizationScopeFactory` quedó como `IContextScopeFactory(userId, organizationId)` y reemplaza también el uso de variables locales a la transacción en los flujos previos a la autenticación (design, decisiones 4 y 8).
- Las funciones `SECURITY DEFINER` necesitan una política `owner_lookup` de solo lectura para `nexgate_owner`, porque `FORCE` aplica RLS también al dueño (design, decisión 6).
- Una renovación sin organización elegida conserva la de la sesión solo si la membresía sigue activa; si no, emite tokens sin organización activa.
- El escenario "Active organization that does not exist" ya no se puede producir por la API (la membresía lo impide); su prueba borra la organización como owner.
- Sin acceso anónimo: las operaciones de organización sin token responden 401 `authentication_required`; con token y sin organización activa, 403 `organization_context_required`.
- Verificación final: build limpio sin warnings, 207 pruebas en verde (10 de arquitectura y 197 del host contra PostgreSQL 16 real), y prueba manual con la API levantada: comando `create-platform-operator`, alta de organización, aceptación de la invitación, inicio de sesión, alta y listado de sitios, `GET /me`, y 401 sin token aunque venga el encabezado viejo. Los logs de esa corrida no tienen correos ni contraseñas.

## Workflow follow-up

- Al archivar: actualizar `docs/02-dominio-y-modelo-de-datos.md` con el schema `identity` real, marcar el cambio 3 en `docs/08` y reemplazar su nota pendiente por la de `add-spaces-and-access-points`: agregar el alcance por espacio a `role-scopes`.
