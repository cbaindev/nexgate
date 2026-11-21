# Tasks

## 1. Merge de la rama de módulos

- [x] 1.1 Ejecutar `git merge --no-ff --no-commit claude/continuacion-sesion-83543c` y resolver según D1 de `design.md`: `--ours` en los archivos que tocaron las dos ramas (salvo `openspec/specs/platform-api/spec.md` y `docs/01-decisiones.md`), y los archivos nuevos en su ruta vieja; verificar con `git status` sin conflictos pendientes
- [x] 1.2 Resolver `openspec/specs/platform-api/spec.md` como la unión de D9 (requirements de SMT + los tres de OpenAPI) y agregar D-34 en `docs/01-decisiones.md` con la nota sobre su número anterior; verificar con `openspec validate --specs --strict` y revisando el diff
- [x] 1.3 Para cada archivo resuelto con `--ours`, listar con `git diff 4ae4a1e claude/continuacion-sesion-83543c -- <archivo>` los cambios de la otra rama y anotar en este archivo, debajo de esta tarea, a qué tarea se portan; verificar que ningún cambio queda sin tarea
  - `ProblemDetailsSetup` → `WebDefaultsExtensions`: números JSON estrictos, `AddNexgateRequestContext`, `AddAuthentication`, `AddRateLimiter`, `code` con `TryAdd` y el orden del pipeline (autenticación → contexto → telemetría → rate limiting): tareas 2.1, 2.2 y 4.4 (en MVC, números estrictos en `JsonOptions` de controllers)
  - `TelemetryEnrichmentMiddleware`: también agrega `UserId` (seudónimo) a logs y trazas: tarea 2.2
  - `Program.cs`: migrador, módulos, comandos `migrate` y `create-platform-operator`, OpenAPI y Swagger UI: tareas 4.4 y 6.1
  - `ArchitectureTests` (descubrimiento con Tenancy e Identity): tarea 7.2
  - Pruebas de SMT que la otra rama adaptó (`CorrelationTests`, `ErrorTests`, `TelemetryTests`, `VersioningTests`, `NexgateFactory`, `HealthTests`, `LogCapture`; sus versiones quedaron en `tests/Nexgate.Host.Tests`): tarea 5.4
  - `Nexgate.slnx`: tarea 5.6. `README.md`, `docs/05`, `docs/08`: tarea 8.1. `dotnet-tools.json` (dotnet-ef) y `appsettings.json`, `infra/` y `docs/02`: entraron con el merge sin conflicto
- [x] 1.4 Confirmar el commit de merge; verificar `dotnet build` y `dotnet test` en verde (los proyectos traídos todavía no están en `Nexgate.slnx`)

## 2. Piezas transversales y control de acceso

- [x] 2.1 Mover con `git mv` `Access/RequestContext` y la persistencia compartida (`Persistence/*`) a `Nexgate.Common.BuildingBlocks/Security` y `/Persistence` según D8, con paquetes EF Core y Npgsql y sin `Microsoft.AspNetCore.App`; unificar `ICurrentOrganization` con `RequestContext` y eliminar `HttpContextCurrentOrganization`; verificar `dotnet build` y que `TelemetryTests` sigue en verde
- [x] 2.2 Mover `RequestContextMiddleware` a `Api.Rest/Middlewares` y registrarlo en el pipeline en el mismo orden que en la rama de módulos; verificar `dotnet build` y las pruebas de correlación y telemetría
- [ ] 2.3 Crear los atributos de acceso de D6 (`RequireAuthenticatedUser`, `RequirePermission`, `RequirePlatformOperator`, `AnonymousAccess`) en `Api.Rest/Authorization`, escribiendo problem details con `IProblemDetailsService`; verificar con pruebas sobre un controller de prueba que cubran 401, 403 `organization_context_required` y 403 `permission_denied` en ese orden
- [x] 2.4 Agregar la clase base `ApiControllerBase` con `[ApiController]`, `[Consumes("application/json")]` y `[Produces("application/json")]`; verificar `dotnet build`

## 3. Tenancy en capas

- [ ] 3.1 Mover con `git mv` `Tenancy.Domain` → `05.Domain/Nexgate.Domain.Tenancy` y `Tenancy.Contracts` → `03.Service/Nexgate.Service.Tenancy.Contracts`, con namespaces `Nexgate.<Capa>.Tenancy`; agregar `ITimeZoneCatalog` a `Contracts` (D5); verificar `dotnet build`
- [ ] 3.2 Crear `04.Data/Nexgate.Data.Tenancy` con `TenancyDbContext`, stores, migraciones (movidas con `git mv`, sin regenerar) y los puertos `IOrganizationStore` e `ISiteStore` (D3); verificar `dotnet build` y `dotnet ef migrations has-pending-model-changes` sin cambios pendientes
- [ ] 3.3 Crear `03.Service/Nexgate.Service.Tenancy` con `OrganizationService`, `SiteService`, `TimeZones`, la implementación de `ITimeZoneCatalog`, `IOrganizationDirectory` e `ISiteDirectory`, todo `internal`, y `AddTenancyService()`; verificar `dotnet build` y que la base de pruebas creada desde cero tiene el schema `tenancy` con sus políticas RLS (`IsolationCatalogTests`)

## 4. Identity en capas

- [ ] 4.1 Mover `Identity.Domain` → `05.Domain/Nexgate.Domain.Identity` y `Identity.Contracts` → `03.Service/Nexgate.Service.Identity.Contracts`; agregar `IPlatformOperators` a `Contracts` (D4); verificar `dotnet build`
- [ ] 4.2 Crear `04.Data/Nexgate.Data.Identity` con `IdentityDbContext`, stores, `IdentityIsolationSql`, migraciones (con `git mv`, sin regenerar), los puertos y records de `IdentityPorts` y las consultas que usan la resolución de acceso y el operador de plataforma (D2, D3); verificar `dotnet build` y `has-pending-model-changes` sin cambios pendientes
- [ ] 4.3 Crear `03.Service/Nexgate.Service.Identity` con los servicios, `OrganizationNames`, `IdentityAccessResolver`, `IdentityPlatformAccess`, hash de contraseñas, emisión de JWT, clave de firma, opciones y su validador (con `Microsoft.Extensions.Identity.Core` y `Microsoft.IdentityModel.JsonWebTokens`, sin `Microsoft.AspNetCore.App`), y `AddIdentityService()`; verificar `dotnet build` y `IdentityUnitTests` movida a `Nexgate.Test.Api` en verde
- [ ] 4.4 Mover a `Api.Rest/Extensions` la configuración de JWT bearer y de rate limiting (D8), y a `Api.Rest/Commands` los comandos `migrate` y `create-platform-operator` usando `IPlatformOperators`; verificar `dotnet run --project src/02.Api/Nexgate.Api.Rest -- migrate` contra la base de `docker compose` y la prueba del comando de operador

## 5. Controllers de Tenancy e Identity

- [ ] 5.1 Crear `Api.Rest/Models/Tenancy` e `Identity` con los modelos de solicitud y los atributos de validación (`IanaTimeZone` con `ITimeZoneCatalog`, `AccountEmail` con `EmailRules`, `JsonObjectOrNull`), con los atributos sobre los parámetros del constructor (D5); verificar `dotnet build`
- [ ] 5.2 Crear `Controllers/Tenancy` (`PlatformController`, `OrganizationController`, `SitesController`) con las mismas rutas, códigos y cuerpos que `MapTenancyEndpoints` y su atributo de acceso por acción; mover a `Nexgate.Test.Api` `TenancyApiTests` y `TenantIsolationTests`; verificar que pasan sin cambiar sus aserciones
- [ ] 5.3 Crear `Controllers/Identity` (`AuthController`, `MeController`, `InvitationsController`, `MembersController`) con las mismas rutas, códigos y cuerpos que `MapIdentityEndpoints`, `[EnableRateLimiting]` en `token` y `invitations/accept`; mover `AuthenticationTests`, `MembershipTests`, `RoleScopeTests`, `AccessResolutionTests` e `IdentityIsolationTests`; verificar que pasan sin cambiar sus aserciones
- [ ] 5.4 Pasar `TestEndpoints` a un controller de prueba registrado como application part desde `Nexgate.Test.Api`, unificar `NexgateFactory` y `TestDatabase` de las dos ramas, y mover las pruebas de salud, errores, versionado, correlación y telemetría de `Sample` a endpoints de Tenancy o de prueba; verificar `dotnet test` en verde
- [ ] 5.5 Agregar la prueba de JSON mal formado sin token en un endpoint protegido (→ 401, ver Risks de `design.md`); verificar con `dotnet test`
- [ ] 5.6 Eliminar `Sample` (proyectos, controller, registro y pruebas) y los proyectos vacíos de `src/Modules`, `src/BuildingBlocks` y `tests/`; reescribir `Nexgate.slnx`; verificar `dotnet build` sin warnings y `git grep -n "Sample\|src/Modules\|Nexgate.Modules\|tests/" -- src Nexgate.slnx` sin resultados

## 6. Contrato OpenAPI sobre MVC

- [ ] 6.1 Mover `OpenApiSetup` y sus transformadores a `Api.Rest/Extensions/OpenApi`, agregar `Asp.Versioning.Mvc.ApiExplorer` con sustitución de versión en la URL y servir el documento y Swagger UI solo en desarrollo (D7); verificar con las pruebas de referencia interactiva (404 fuera de desarrollo)
- [ ] 6.2 Declarar en cada acción `EndpointName`, `EndpointSummary`, `EndpointDescription`, `Tags` y sus `ProducesResponseType`, y agregar la convención de D7 (500, 400 con cuerpo, 401/403 por atributo de acceso, 429 por rate limiting, marca de token); verificar con `OpenApiContractTests` contra `api/nexgate-v1.openapi.json` sin modificar el archivo
- [ ] 6.3 Si la prueba de contrato muestra diferencias, corregirlas en el código; si alguna no se puede eliminar sin cambiar el comportamiento, detenerse y actualizar proposal y design antes de seguir; verificar con `git diff claude/continuacion-sesion-83543c -- api/` vacío

## 7. Reglas de arquitectura

- [ ] 7.1 Reescribir `ArchitectureRules` con la tabla de D4 (contracts del propio módulo desde cualquier capa, entre módulos solo `Service → Service.<Otro>.Contracts`, módulos sin `Microsoft.AspNetCore.App`, `Data.<M>` solo desde `Service.<M>` y `02.Api`); verificar con pruebas sobre grafos sintéticos, una por regla, que fallan ante la referencia prohibida
- [ ] 7.2 Actualizar la prueba de descubrimiento para exigir las cuatro capas de Tenancy e Identity además de `Api.Rest`, `Api.Workers` y `Common.BuildingBlocks`; verificar con `dotnet test` y comprobando que falla si se renombra una carpeta
- [ ] 7.3 Ampliar la prueba por reflexión de controllers para aceptar `Nexgate.Api.Rest.Models` (D5) y agregar la prueba de requisito de acceso (D6); verificar con controllers de prueba que usan un tipo de `Data` y que no tienen atributo de acceso, y que ambas reglas fallan

## 8. Documentación y cierre

- [ ] 8.1 Actualizar `docs/02-dominio-y-modelo-de-datos.md` y `docs/05-arquitectura-tecnica.md` (ubicación de cada pieza de los módulos, puertos en `Data`, control de acceso con atributos, OpenAPI sobre MVC), `docs/08-plan-de-cambios-openspec.md` (agregar este cambio y marcar 2 y 3 como implementados), `README.md`, `CLAUDE.md` y el contexto de `openspec/config.yaml`; verificar con `git grep -n "src/Modules\|Application\|Infrastructure\|MapGroup\|RequirePermission(" docs README.md CLAUDE.md openspec/config.yaml` sin referencias a la estructura vieja
- [ ] 8.2 Revisar las referencias a "D-32" en `docs/`, `README.md`, `CLAUDE.md` y `openspec/specs` y corregir las que hablan del contrato OpenAPI a D-34; verificar con `git grep -n "D-32"` revisando cada resultado
- [ ] 8.3 Build y pruebas desde cero (`git clean -xdf` de `bin/obj`, base recreada con `docker compose`, `dotnet build`, `dotnet test`); verificar ambos en verde, que `git diff claude/continuacion-sesion-83543c -- api/` está vacío y que `git diff -M claude/continuacion-sesion-83543c` sobre las migraciones solo muestra renombres y cambios de `namespace`/`using`
- [ ] 8.4 Validar el cambio con `openspec validate integrate-modules-into-layers --strict`; verificar que no reporta errores
