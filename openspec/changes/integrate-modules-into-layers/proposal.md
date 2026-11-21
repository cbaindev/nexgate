# Proposal

## Why

El desarrollo se separó en dos ramas a partir de `add-solution-foundation`:

- `adopt-smt-layout-and-controllers` (archivado el 2026-11-11, rama `claude/nexgate-smt-migration-120a9c`) movió la solución a capas numeradas al estilo SMT (`02.Api`, `03.Service`, `04.Data`, `05.Domain`, `06.Common`, `07.Test`) con controllers (D-32, D-33). Daba por hecho que Tenancy llegaría después, ya con la estructura nueva.
- En paralelo, `add-tenancy-and-isolation`, `add-identity-and-roles` y `add-openapi-contract` (archivados el 2026-11-07, rama `claude/continuacion-sesion-83543c`) se implementaron sobre la estructura anterior: `src/Modules/<Modulo>/<Modulo>.{Contracts,Domain,Application,Infrastructure}`, endpoints Minimal API dentro de cada módulo y un `BuildingBlocks` que mezcla piezas HTTP con piezas transversales.

Ninguna rama tiene todo, y las dos chocan en la ubicación de los proyectos, en el estilo de endpoints y en la numeración de decisiones (hay dos D-32 distintas). Cuanto más se demore, más caro es: `add-spaces-and-access-points` ya necesita Tenancy e Identity y la estructura SMT a la vez. Se conserva la estructura SMT, que es la decisión vigente (D-32, D-33), y se llevan a ella Tenancy, Identity y el contrato OpenAPI sin cambiar el comportamiento observable.

## What Changes

- **Merge** de `claude/continuacion-sesion-83543c` sobre la rama SMT, para conservar el historial de ambas; después, `git mv` a la estructura nueva. En los archivos que tocaron las dos ramas se toma la versión SMT y se portan los cambios de la otra rama en los grupos de tareas.
- **Tenancy e Identity en capas** (D-32): `Nexgate.Domain.<M>` (entidades y reglas), `Nexgate.Data.<M>` (DbContext, stores, migraciones existentes, SQL de aislamiento), `Nexgate.Service.<M>` (servicios de aplicación, implementación de los contratos, hash de contraseñas y emisión de tokens) y `Nexgate.Service.<M>.Contracts` (interfaces públicas, DTOs, nombres de permisos, roles y alcances, códigos de error). Se elimina el módulo de ejemplo `Sample`.
- **Endpoints como controllers** (D-33) en `Nexgate.Api.Rest/Controllers/<Modulo>/`, con las mismas rutas, códigos de estado y cuerpos. El control de acceso pasa de filtros de endpoint a atributos de autorización MVC que devuelven los mismos códigos de error (`authentication_required`, `organization_context_required`, `permission_denied`).
- **`Nexgate.Common.BuildingBlocks` sin HTTP**: recibe el contexto de seguridad de la solicitud (usuario, organización activa, permisos efectivos) y la persistencia compartida (DbContext base, interceptor de sesión para RLS, migrador, roles de base). A **`Nexgate.Api.Rest`** van el middleware de contexto, la validación de JWT, el rate limiting, OpenAPI, Swagger UI y los comandos administrativos (`migrate`, `create-platform-operator`).
- **Contrato OpenAPI sin cambios**: `api/nexgate-v1.openapi.json` queda idéntico byte a byte y lo sigue verificando la prueba de contrato. Una convención de MVC agrega las respuestas 400/401/403/429/500 a partir de los atributos de cada acción; `Asp.Versioning.Mvc.ApiExplorer` sustituye la versión en la URL.
- **Reglas de arquitectura ampliadas**: cualquier capa de un módulo puede usar el `Contracts` de su propio módulo (por ejemplo, `Domain.Identity` usa los nombres de permisos, roles y alcances); entre módulos solo `Service.<M> → Service.<Otro>.Contracts`. Ningún proyecto de módulo usa el framework web. Los puertos de persistencia viven en `Data.<M>`; lo que cruza módulos (por ejemplo, el nombre de una organización que Identity le pide a Tenancy) vive en `Service.<M>`. Prueba nueva: toda acción de un controller declara su requisito de acceso, aunque sea anónimo.
- **Specs y documentación**: `platform-api` combina las precisiones de SMT (versión no soportada, reglas de capas) con los requirements de OpenAPI; los specs `authentication`, `organization-membership`, `organizations-and-sites`, `role-scopes` y `tenant-isolation` llegan sin cambios. La decisión del contrato OpenAPI pasa de D-32 a **D-34**. Se actualizan `docs/02`, `docs/05`, `docs/08`, `README.md`, `CLAUDE.md` y el contexto de `openspec/config.yaml`.

### Fuera de alcance

- `add-spaces-and-access-points` y cualquier módulo nuevo.
- Cambios de contrato HTTP, de esquema de base, de políticas RLS o de migraciones (las migraciones existentes se mueven sin regenerarse).
- Reescribir los cambios archivados de cualquiera de las dos ramas (siguen citando la estructura y la numeración de su momento).
- Abrir el PR a `dev` o `main`: se decide después de implementar.
- Frontend y cliente TypeScript generado.

## Capabilities

### New Capabilities

(ninguna)

### Modified Capabilities

- `platform-api`: el aislamiento entre módulos suma que la persistencia de un módulo no se accede desde otro módulo ni desde la API sin pasar por su capa de servicio.

## Impact

- **Código**: se mueven con `git mv` los proyectos de `src/Modules/Tenancy`, `src/Modules/Identity` y las piezas de `src/BuildingBlocks` que trae la otra rama; se reescriben los endpoints como controllers; se reescribe `Nexgate.slnx`; las pruebas de `tests/` de la otra rama pasan a `src/07.Test/Nexgate.Test.Api` y `Nexgate.Test.Architecture`. Se elimina `Sample` (proyectos, controller y pruebas).
- **Dependencias**: se agrega `Asp.Versioning.Mvc.ApiExplorer`. Los paquetes de EF Core y Npgsql pasan a `Common` y `Data`; JwtBearer, OpenAPI y Swagger UI quedan solo en `Nexgate.Api.Rest`.
- **Contrato HTTP y datos**: sin cambios, con una excepción acotada: un endpoint protegido que recibe JSON mal formado sin token pasa de 400 a 401, porque en MVC la autorización corre antes de leer el cuerpo (ver `design.md`, Risks). Además, la API acepta cuerpos con `Content-Type: text/json`, que Minimal API rechazaba con 415. El contrato sigue declarando solo `application/json` (ver `design.md`, D7). Lo demás lo confirman las mismas pruebas de integración con las mismas aserciones, la prueba del contrato OpenAPI y la ausencia de migraciones nuevas.
- **Seguridad, privacidad y multitenancy**: no cambia ninguna garantía. El aislamiento por organización sigue en RLS y en el interceptor de sesión, que solo cambian de proyecto; las pruebas de aislamiento entre organizaciones, de roles por alcance y de autenticación se mantienen sin tocar sus aserciones. El riesgo nuevo es que un controller quede sin control de acceso al pasar de filtros de endpoint a atributos: lo cubre la prueba de que toda acción declara su requisito de acceso, y la regla nueva de persistencia evita que la API o un módulo se salten la capa de servicio. Los logs siguen sin datos personales.
- **Decisiones relacionadas**: aplica D-32 y D-33 a Tenancy e Identity; renumera la decisión del contrato OpenAPI como D-34; mantiene D-02, D-03, D-04, D-05, D-27 y D-28.
