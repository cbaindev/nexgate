# Design

## Context

La motivación está en `proposal.md`. Estado de partida:

- **Rama SMT** (`claude/integrate-modules-into-layers`, desde `claude/nexgate-smt-migration-120a9c`): capas numeradas con `Nexgate.Api.Rest` (controllers, `Middlewares/`, `Extensions/`), `Nexgate.Api.Workers`, `Nexgate.Common.BuildingBlocks` sin ASP.NET Core, el módulo de ejemplo `Sample` y las pruebas en `src/07.Test`. `Asp.Versioning.Mvc` con versión no soportada → 404. Reglas de arquitectura por `.csproj` (tabla D2 de `adopt-smt-layout-and-controllers`) y por reflexión sobre los controllers (D3 del mismo cambio).
- **Rama de módulos** (`claude/continuacion-sesion-83543c`): `src/Modules/Tenancy` y `src/Modules/Identity` con cuatro proyectos cada uno (`Contracts`, `Domain`, `Application`, `Infrastructure`), migraciones EF Core de los schemas `tenancy` e `identity`, endpoints Minimal API dentro de `*.Infrastructure` (`MapTenancyEndpoints`, `MapIdentityEndpoints`), filtros de acceso (`RequireAuthenticatedUser`, `RequirePermission`, `RequirePlatformOperator`), rate limiting de los endpoints de autenticación, OpenAPI con `Microsoft.AspNetCore.OpenApi` + Swagger UI y la prueba que compara el documento generado con `api/nexgate-v1.openapi.json`.
- Dependencias de la rama de módulos que no encajan en la tabla SMT: `Infrastructure → Application` (los puertos de persistencia están en `Application`), `Identity.Domain → Identity.Contracts`, `Tenancy.Infrastructure → Identity.Contracts` (para los nombres de permisos de los endpoints), y `FrameworkReference Microsoft.AspNetCore.App` en los `*.Infrastructure` y en `BuildingBlocks`.

## Goals / Non-Goals

**Goals:**

- Una sola rama con Tenancy, Identity y OpenAPI sobre la estructura SMT, sin cambios observables: mismas rutas, códigos, cuerpos, contrato OpenAPI y esquema de base.
- Que las garantías de seguridad (autenticación, organización activa, permisos por alcance, RLS) se sigan verificando con las mismas pruebas y las mismas aserciones.
- Reglas de arquitectura que reflejen la estructura real con módulos con persistencia, que la de SMT solo anticipaba.

**Non-Goals:**

- Mejorar el diseño interno de los módulos más allá de lo que exige la estructura (se mueve código, no se rediseña).
- Regenerar migraciones o tocar políticas RLS.

## Decisions

### D1. Merge con historial y resolución a favor de SMT

`git merge --no-ff claude/continuacion-sesion-83543c` sobre la rama SMT. Como las dos ramas renombraron o modificaron los mismos archivos de `add-solution-foundation` (`Program.cs`, `BuildingBlocks/*`, `Nexgate.slnx`, pruebas, `docs/`, `openspec/specs/platform-api/spec.md`), la resolución es:

- **Archivos que tocaron las dos ramas**: se toma la versión SMT (`git checkout --ours`) y los cambios de la otra rama se portan en el grupo de tareas correspondiente, leyéndolos con `git show claude/continuacion-sesion-83543c:<ruta>`. Excepciones que se resuelven en el mismo merge porque son texto: `openspec/specs/platform-api/spec.md` (requirements de SMT + los tres de OpenAPI, ver D9) y `docs/01-decisiones.md` (D-34, ver D9).
- **Archivos que solo trae la otra rama** (`src/Modules/**`, `src/BuildingBlocks/{Access,OpenApi,Persistence,...}`, `tests/**`, `api/`, specs nuevos, cambios archivados): entran en su ruta vieja y se mueven con `git mv` en los grupos siguientes.
- El commit de merge compila: los proyectos traídos todavía no están en `Nexgate.slnx`. Desde ahí, cada grupo de tareas termina con `dotnet build` y `dotnet test` en verde.

- **Alternativa descartada: rebase o cherry-pick de los commits de la otra rama.** Reaplicar commits sobre una estructura distinta produce conflictos en cada commit y reescribe historial ya publicado.
- **Alternativa descartada: copiar los archivos sin merge.** Más simple, pero se pierde el historial de Tenancy e Identity y ambas ramas quedan sin un ancestro común para el PR final.

### D2. Ubicación de cada pieza de los módulos

| Pieza (rama de módulos) | Destino |
|---|---|
| `<M>.Domain` (entidades, reglas, `PermissionCatalog`) | `05.Domain/Nexgate.Domain.<M>` |
| `<M>.Contracts` (DTOs, interfaces públicas, permisos, roles, alcances, códigos de error, `EmailRules`) | `03.Service/Nexgate.Service.<M>.Contracts` |
| `<M>.Infrastructure`: DbContext, stores, migraciones, `*IsolationSql` | `04.Data/Nexgate.Data.<M>` |
| `<M>.Application`: puertos de persistencia (`IAccountStore`, `ISiteStore`, `IIdentityLookups`, …) y sus records | `04.Data/Nexgate.Data.<M>`, junto a su implementación (D3) |
| `<M>.Application`: servicios (`OrganizationService`, `SiteService`, `AuthenticationService`, `InvitationService`, `MemberService`, `CurrentUserService`, `PlatformOperatorService`), `TimeZones` | `03.Service/Nexgate.Service.<M>` (`internal`, expuestos por interfaces de `Contracts`) |
| `<M>.Infrastructure`: hash de contraseñas, emisión de JWT, clave de firma, opciones y su validador | `Nexgate.Service.Identity` (usan `Microsoft.Extensions.Identity.Core` y `Microsoft.IdentityModel.JsonWebTokens`, no el framework web) |
| Implementaciones que cruzan módulos (`OrganizationNames` usa `IOrganizationDirectory` de Tenancy) | `Nexgate.Service.<M>` |
| Implementaciones de interfaces de `Common` que resuelven acceso (`IdentityAccessResolver`, `IdentityPlatformAccess`) | `Nexgate.Service.Identity`, apoyadas en consultas de `Data.Identity` |
| `Map<M>Endpoints`, modelos de solicitud con DataAnnotations y sus atributos de validación | `02.Api/Nexgate.Api.Rest/Controllers/<M>/` y `Models/<M>/` (D5, D6) |
| Registro DI (`Add<M>Module`) | `Add<M>Service()` en `Nexgate.Service.<M>` (registra también `Data.<M>`), invocado desde `Api.Rest/Extensions/ModuleExtensions` |

Los nombres de ensamblado y namespaces pasan de `Nexgate.Modules.<M>.<Capa>` a `Nexgate.<Capa>.<M>`. Las migraciones se mueven sin regenerarse: el `MigrationId` y el snapshot no dependen del namespace, y el `__EFMigrationsHistory` de cada schema conserva sus filas. Se verifica con `dotnet ef migrations has-pending-model-changes` por DbContext y con la base de pruebas creada desde cero.

### D3. Puertos de persistencia en `Data`, sentido `Service → Data`

La rama de módulos tiene los puertos en `Application` e `Infrastructure → Application`. La tabla de D-32 fija `Service → Data`. Se mueven las interfaces y sus records a `Data.<M>`; los servicios las consumen igual que antes, solo cambia el `using`. Las implementaciones son `internal` y se registran desde `Add<M>Service()`.

- **Alternativa descartada: mantener la inversión (`Data` implementa puertos de `Service`) solo para estos módulos.** `adopt-smt-layout-and-controllers` (D2) dejó abierta esa puerta para un módulo puntual, pero aplicarla a los dos primeros módulos reales la convierte en la regla de hecho y deja dos convenciones conviviendo. Las reglas de negocio que necesitan pruebas puras ya están en `Domain`.

### D4. Reglas de dependencia ampliadas

Se reescribe la tabla de D2 de `adopt-smt-layout-and-controllers`:

| Proyecto | Puede referenciar |
|---|---|
| `Nexgate.Service.<M>.Contracts` | nada (sin proyectos, paquetes ni frameworks; solo la BCL) |
| `Nexgate.Domain.<M>` | `Service.<M>.Contracts` del propio módulo |
| `Nexgate.Data.<M>` | `Domain.<M>`, `Service.<M>.Contracts` del propio módulo, `Common.*` |
| `Nexgate.Service.<M>` | `Service.<M>.Contracts`, `Domain.<M>`, `Data.<M>`, `Service.<Otro>.Contracts`, `Common.*` |
| `Nexgate.Common.*` | otros `Common.*`; nunca módulos ni `02.Api`; sin `Microsoft.AspNetCore.App` |
| `Nexgate.Api.Rest` / `Nexgate.Api.Workers` | todo lo anterior (raíz de composición) |

Reglas nuevas respecto de SMT:

- **Contracts del propio módulo desde cualquier capa.** `Domain.Identity` usa `Permissions`, `RoleNames` y `ScopeTypeNames`, que son a la vez valores públicos de la API y reglas del dominio. Duplicarlos en `Domain` obligaría a mantener dos listas sincronizadas.
- **Entre módulos, solo `Service → Service.<Otro>.Contracts`.** `Domain` y `Data` de un módulo no ven a otros módulos, ni siquiera sus contratos. `Tenancy.Infrastructure → Identity.Contracts` desaparece porque los permisos los exigen los controllers.
- **Ningún proyecto de módulo usa `Microsoft.AspNetCore.App`.** Garantiza que los endpoints, el JWT bearer y el rate limiting vivan solo en `Api.Rest`.
- **Persistencia solo por su servicio** (requirement modificado de `platform-api`): `Data.<M>` solo puede ser referenciado por `Service.<M>` y por `02.Api` para composición; la prueba por reflexión de los controllers (D3 de SMT) ya impide que un controller use un tipo de `Data`. Para los comandos administrativos de `Api.Rest` se aplica la misma regla: usan interfaces de `Contracts` (`IPlatformOperators`, nuevo en `Service.Identity.Contracts`, que reemplaza el uso directo de `PlatformOperatorService`).

La prueba de descubrimiento pasa a exigir `Api.Rest`, `Api.Workers`, `Common.BuildingBlocks` y las cuatro capas de Tenancy e Identity, en lugar de `Sample`.

- **Alternativa descartada: permitir `Contracts → Contracts` de otro módulo.** Resolvería que el modelo de alta de organización valide el correo con `EmailRules` de Identity, pero abre la puerta a ciclos entre contratos y a que los contratos de un módulo arrastren los de otros. Se resuelve ubicando los modelos de solicitud en la API (D5).

### D5. Modelos de solicitud HTTP en `Api.Rest/Models/<M>`

Los records de solicitud (`CreateOrganizationRequest`, `CreateSiteRequest`, `SignInRequest`, `InviteRequest`, …) son la forma HTTP de cada operación: llevan `JsonElement`, mensajes de validación en español y validaciones que cruzan módulos (el alta de organización valida el correo del administrador con las reglas de Identity). Se ubican en `Nexgate.Api.Rest/Models/<M>/`; el controller los traduce a los tipos de entrada de `Contracts` (`SiteInput`, etc.), como hoy hacen las lambdas de Minimal API. Las respuestas y DTOs siguen en `Contracts`.

- Los atributos de validación se reescriben para MVC con los atributos sobre el parámetro del constructor (no `[property: ...]`, que MVC rechaza en records, ver D5 de SMT).
- `IanaTimeZone` necesita la base tzdb de NodaTime, que vive en `Service.Tenancy`. El atributo resuelve `ITimeZoneCatalog` (interfaz nueva en `Service.Tenancy.Contracts`, implementada con `TimeZones` en `Service.Tenancy`) con `ValidationContext.GetService`, que en MVC entrega los servicios de la solicitud. `AccountEmail` usa `EmailRules` de `Service.Identity.Contracts`.
- La prueba por reflexión de los controllers acepta, además de `*.Contracts`, `Common.*` y el framework, los tipos del namespace `Nexgate.Api.Rest.Models`.

- **Alternativa descartada: modelos de solicitud en `Contracts`, como `GreetRequest` en SMT.** `Contracts` no puede referenciar NodaTime ni los contratos de otro módulo (D4), y los modelos de Sample no tenían esas dependencias.

### D6. Control de acceso con atributos de autorización MVC

Los filtros de endpoint pasan a atributos en `Nexgate.Api.Rest/Authorization/`, aplicables a controller o acción, implementados como `IAsyncAuthorizationFilter` (se ejecutan antes del model binding y de la validación, igual que hoy el filtro de acceso corre antes que el de validación):

| Minimal API | MVC | Respuestas |
|---|---|---|
| `RequireAuthenticatedUser()` | `[RequireAuthenticatedUser]` | 401 `authentication_required` |
| `RequirePermission(p, atOrganization)` | `[RequirePermission(Permissions.X, AtOrganization = true)]` | 401, 403 `organization_context_required`, 403 `permission_denied` (en ese orden) |
| `RequirePlatformOperator()` | `[RequirePlatformOperator]` | 401, 403 `permission_denied` |
| sin filtro (token, refresh, logout) | `[AnonymousAccess]` | — |
| `AcceptsAccessToken(required: false)` (aceptar invitación) | `[AnonymousAccess(AcceptsAccessToken = true)]` | — |

Los problem details se escriben con `IProblemDetailsService`, así reciben `code` y `correlationId` igual que el resto de los errores. El filtrado por sitio dentro del alcance sigue en cada acción con `ICurrentPermissions.SitesFor`, como hoy. `RequestContext`, `ICurrentUser`, `ICurrentPermissions`, `IPlatformAccess`, `IRequestAccessResolver`, `IContextScopeFactory` y `AccessErrorCodes` pasan a `Common.BuildingBlocks/Security` (no dependen de HTTP); `RequestContextMiddleware` y los atributos, a `Api.Rest`.

Prueba de arquitectura nueva: toda acción pública de un controller tiene exactamente un atributo de acceso (propio o heredado del controller). Una acción nueva sin atributo hace fallar el build, en lugar de quedar pública por omisión.

- **Alternativa descartada: autorización por políticas de ASP.NET Core (`[Authorize(Policy = ...)]`).** Es el mecanismo estándar, pero el desafío y la prohibición por defecto no distinguen `organization_context_required` de `permission_denied`; habría que reescribir las respuestas en un `IAuthorizationMiddlewareResultHandler` que adivina la causa a partir del requisito fallido. Los atributos propios mantienen el orden de verificación y los códigos explícitos en un solo lugar.
- **Alternativa descartada: un filtro global que deniegue por defecto.** Protege igual ante una acción sin atributo, pero la decisión queda implícita; la prueba de arquitectura da el mismo resultado y deja visible el requisito en cada acción.

### D7. OpenAPI sobre MVC con el mismo documento

- Se mantiene `Microsoft.AspNetCore.OpenApi` con los mismos transformadores de schema, operación y documento (problem details, token de acceso, orden estable), movidos a `Api.Rest/Extensions/OpenApi/`. Se agrega `Asp.Versioning.Mvc.ApiExplorer` con `GroupNameFormat = "'v'VVV"` y `SubstituteApiVersionInUrl = true`, para que las rutas salgan como `/api/v1/...` y no como `/api/v{version}/...`.
- Los metadatos de cada operación se declaran con atributos: `[EndpointName]` (operationId), `[EndpointSummary]`, `[EndpointDescription]`, `[Tags]` y `[ProducesResponseType]` para las respuestas de éxito y los errores propios de la acción (404, 409).
- Una `IApplicationModelConvention` agrega lo que hoy agregan `Problems(...)` y los filtros: 500 a toda acción, 400 a las acciones con cuerpo, 401/403 según el atributo de acceso (D6), 429 a las acciones con `[EnableRateLimiting]`, y la marca de token de acceso (requerido u opcional) que lee el transformador de operación.
- Para que los tipos de contenido coincidan con los de Minimal API, los controllers declaran `[Consumes("application/json")]` y `[Produces("application/json")]` en una clase base `ApiControllerBase`; si no, MVC anuncia también `text/json` y `application/*+json`.
- La prueba de contrato no cambia: el documento generado debe ser idéntico a `api/nexgate-v1.openapi.json`. Si en la implementación aparece una diferencia que no se puede eliminar sin cambiar el comportamiento, se detiene el cambio y se actualizan los artefactos antes de tocar el archivo versionado.

- **Alternativa descartada: Swashbuckle para generar el documento.** Es lo que usa SMT, pero el contrato de la rama de módulos ya está producido y verificado con `Microsoft.AspNetCore.OpenApi`; cambiar de generador garantiza diferencias en el archivo versionado. Swashbuckle se sigue usando solo para la UI (`Swashbuckle.AspNetCore.SwaggerUI`).

### D8. Piezas transversales: qué queda en `Common` y qué va a `Api.Rest`

| Pieza (rama de módulos) | Destino |
|---|---|
| `Access/RequestContext` (contexto, permisos efectivos, fábrica de scopes) | `Common.BuildingBlocks/Security` |
| `Access/AccessEndpointFilters` | atributos en `Api.Rest/Authorization` (D6); `AccessErrorCodes` e `IPlatformAccess` en `Common.BuildingBlocks/Security` |
| `Access/RequestContextMiddleware` | `Api.Rest/Middlewares` |
| `Persistence/*` (DbContext base, `SessionContextInterceptor`, `DatabaseMigrator`, `DatabaseRoles`, `DatabaseBootstrap`, extensiones de aislamiento para migraciones) | `Common.BuildingBlocks/Persistence` (EF Core y Npgsql, sin ASP.NET Core) |
| `OpenApi/*` | `Api.Rest/Extensions/OpenApi` (D7) |
| `Validation/ValidationFilter` | se elimina (validación automática de `[ApiController]`, D5 de SMT) |
| Rate limiting (hoy en `IdentityModule`) | `Api.Rest/Extensions/RateLimitingExtensions`, leyendo `IdentityModuleOptions.RateLimit` |
| `AddJwtBearer` y su configuración (hoy en `IdentityModule`) | `Api.Rest/Extensions/AuthenticationExtensions`, usando la clave de firma y las opciones públicas de `Service.Identity` |
| `AdminCommands` (`migrate`, `create-platform-operator`) | `Api.Rest/Commands` |
| Piezas que ya existen en las dos ramas (`CorrelationId`, telemetría, health, `ICurrentOrganization`, manejador global de errores) | versión SMT; se portan los cambios de la otra rama si los hay |

`ICurrentOrganization` de SMT y `RequestContext` de la otra rama se unifican: `RequestContext` implementa `ICurrentOrganization` (como ya hace en la rama de módulos) y reemplaza a `HttpContextCurrentOrganization`, que se elimina.

### D9. Specs y numeración de decisiones

- `openspec/specs/platform-api/spec.md` se resuelve en el merge como la unión de los dos: los requirements de SMT (con "Unsupported version" y los escenarios de capas de "Module isolation") más "API description document", "Versioned API contract" e "Interactive API reference in development", y el `Purpose` que menciona la descripción OpenAPI. Este cambio solo agrega el escenario de persistencia a "Module isolation" (delta).
- Los specs que solo trae la otra rama entran tal cual en `openspec/specs/`.
- `docs/01-decisiones.md`: la decisión del contrato OpenAPI de la otra rama se agrega como **D-34**, con una nota de que en `add-openapi-contract` figura como D-32. Los cambios archivados no se reescriben. Se buscan referencias a "D-32" en `docs/`, `README.md`, `CLAUDE.md` y `openspec/specs` para corregir las que hablan del contrato OpenAPI.

## Cómo se prueba

- **Integración con PostgreSQL real** (`Nexgate.Test.Api`, base levantada con `docker compose`): todas las suites de la rama de módulos (autenticación, membresías, roles por alcance, resolución de acceso, aislamiento entre organizaciones de Tenancy e Identity, catálogo de aislamiento, API de Tenancy, contrato OpenAPI) y las de SMT (salud, errores, versionado, correlación, telemetría) pasan sin cambiar sus aserciones; solo cambian namespaces, la factoría de pruebas y los endpoints de prueba (`TestEndpoints` pasa a un controller de prueba registrado como application part desde el proyecto de pruebas). Las pruebas de `Sample` se eliminan con el módulo; la cobertura de versión no soportada y de validación pasa a usar endpoints de Tenancy.
- **Unitarias**: `IdentityUnitTests` y las reglas de dominio, sin cambios.
- **Arquitectura**: grafos sintéticos por cada regla de D4 (incluyendo `Domain → Service.<Otro>.Contracts`, `Data → Service`, `Data.<M>` referenciado por otro módulo y un proyecto de módulo con `Microsoft.AspNetCore.App`, todos rechazados), la prueba de descubrimiento con Tenancy e Identity, la prueba por reflexión de controllers con `Models` y la prueba nueva de requisito de acceso, con un controller de prueba sin atributo que la hace fallar.
- **Contrato y datos**: `OpenApiContractTests` contra el archivo sin modificar; `git diff` contra `claude/continuacion-sesion-83543c` vacío en `api/` y, en las migraciones, solo renombres y cambios de `namespace`/`using`; `dotnet ef migrations has-pending-model-changes` en verde para los dos DbContext.
- **Build**: `dotnet build` sin warnings (`TreatWarningsAsErrors`) y `dotnet test` en verde al cerrar cada grupo de tareas.

## Risks / Trade-offs

- [El documento OpenAPI generado desde MVC difiere del de Minimal API: tipos de contenido, `required`/nullabilidad de parámetros, orden de respuestas, nombres de schema] → `ApiControllerBase` con `Consumes`/`Produces`, la convención de D7 y la prueba de contrato byte a byte. Si queda una diferencia irreducible, se frena y se actualizan los artefactos (D7).
- [Una acción queda sin control de acceso o con uno distinto al original] → prueba de requisito de acceso (D6) más las pruebas de roles por alcance y aislamiento, que ejercitan cada endpoint con usuarios sin permiso.
- [El orden de verificación cambia: el filtro de autorización MVC corre antes del model binding; en Minimal API el binding corre antes de los filtros de endpoint] → para un cuerpo que se deserializa pero no valida, no cambia nada (hoy el filtro de acceso corre antes que el de validación: 401). Sí cambia un cuerpo con JSON mal formado en un endpoint protegido y sin token: Minimal API responde 400 porque falla el binding, MVC responde 401. Es la única diferencia observable prevista; no la cubre ningún requirement ni prueba actual, y responder primero por la autenticación no revela nada del endpoint. Se agrega una prueba que fija el comportamiento nuevo (sin token y JSON mal formado → 401) y se señala en el PR.
- [Los conflictos del merge esconden un cambio de la otra rama en un archivo resuelto con `--ours`] → tarea de revisión con `git diff 4ae4a1e claude/continuacion-sesion-83543c -- <archivo>` para cada archivo resuelto así, listando qué se porta y dónde.
- [Mover migraciones rompe el historial de EF Core] → no se regeneran; verificación con `has-pending-model-changes` y con la base de pruebas desde cero.
- [El merge es grande y difícil de revisar] → commits por grupo de tareas, cada uno compilando y con pruebas en verde (D1).
- [Trade-off: modelos de solicitud en la API en lugar de `Contracts`] → aceptado (D5); la API ya es la dueña de la forma HTTP, y los contratos quedan libres de dependencias.

## Migration Plan

No hay despliegues ni datos productivos. La base local se puede recrear desde cero; una base existente sigue siendo válida porque las migraciones no cambian. Rollback: revertir los commits del cambio en esta rama; `claude/continuacion-sesion-83543c` y `claude/nexgate-smt-migration-120a9c` quedan intactas.
