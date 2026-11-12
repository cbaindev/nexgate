# Proposal

## Why

Nexgate y SMT (ShiftManagment) los mantiene el mismo equipo, pero tienen estructuras de solución distintas: SMT ordena los proyectos en capas numeradas (`01.Client`, `02.Api`, `03.Service`, `04.Data`, `05.Domain`, `06.Common`) con nombres `<Producto>.<Capa>.<Tipo>` y una API REST con controllers; Nexgate agrupa por módulo (`src/Modules/<Modulo>/<Modulo>.<Capa>`) y expone los endpoints con Minimal API. Tener dos convenciones obliga a cambiar de modelo mental al pasar de un repositorio al otro y hace más difícil reutilizar piezas y criterios de revisión.

Hoy Nexgate solo tiene el esqueleto (`add-solution-foundation`) y el módulo de ejemplo `Sample`, sin persistencia ni módulos reales. Es el momento más barato para alinearlo con SMT: después de `add-tenancy-and-isolation` cada módulo nuevo multiplicaría el costo de la migración.

## What Changes

- **Estructura de directorios y proyectos al estilo SMT**, bajo `src/`:
  - `01.Client/`: reservado para el frontend (el panel React llega con `add-admin-panel-shell`; en este cambio solo se documenta).
  - `02.Api/`: procesos de entrada. `Nexgate.Host` pasa a `Nexgate.Api.Rest` y `Nexgate.Workers` pasa a `Nexgate.Api.Workers`.
  - `03.Service/`: lógica de aplicación por módulo. `Sample.Application` pasa a `Nexgate.Service.Sample` y `Sample.Contracts` pasa a `Nexgate.Service.Sample.Contracts`.
  - `04.Data/`: persistencia por módulo (`Nexgate.Data.<Modulo>`). Se crea recién cuando un módulo tiene persistencia; `Sample` no la tiene.
  - `05.Domain/`: dominio por módulo. `Sample.Domain` pasa a `Nexgate.Domain.Sample`.
  - `06.Common/`: piezas transversales sin HTTP. `BuildingBlocks` pasa a `Nexgate.Common.BuildingBlocks`.
  - `07.Test/`: pruebas. `Nexgate.ArchitectureTests` pasa a `Nexgate.Test.Architecture` y `Nexgate.Host.Tests` pasa a `Nexgate.Test.Api`.
- **Se mantiene el monolito modular (D-27)**: dentro de cada capa hay un proyecto por módulo, y un módulo solo depende de otro a través de `Nexgate.Service.<Modulo>.Contracts`. La diferencia con SMT, que usa un único proyecto por capa, es deliberada (ver `design.md`).
- **Sentido de dependencias de SMT**: `Api → Service → Data → Domain`, con `Common` disponible para todos. Reemplaza el sentido actual (`Infrastructure → Application`).
- **API tradicional con controllers en lugar de Minimal API**: los endpoints de negocio pasan a controllers con `[ApiController]` en `02.Api/Nexgate.Api.Rest/Controllers/<Modulo>/`, versionados con atributos de versión de API. Los middlewares, filtros y el registro de módulos viven en `Nexgate.Api.Rest` (`Middlewares/`, `Extensions/`), como en SMT.
- La validación de entrada pasa del filtro de endpoint propio a la validación automática de `[ApiController]`, manteniendo el mismo formato de error.
- Las reglas de dependencia que validan las pruebas de arquitectura se reescriben para la nueva estructura y se agrega una regla para que los controllers solo usen los contratos públicos de los módulos.
- Las convenciones observables de `platform-api` se mantienen; se precisan dos: el comportamiento ante una versión de API no soportada y las reglas de capas dentro de la verificación de aislamiento entre módulos.
- Nuevas decisiones en `docs/01-decisiones.md`: **D-32** (estructura en capas numeradas al estilo SMT con proyectos por módulo) y **D-33** (API REST con controllers). Se actualizan `docs/05-arquitectura-tecnica.md`, `docs/08-plan-de-cambios-openspec.md`, `README.md` y `CLAUDE.md`.

### Lo que no se adopta de SMT

Solo se toma la estructura y el estilo de API. No se adoptan: un único proyecto por capa, el manejador de errores que devuelve el mensaje de la excepción, `Nullable` deshabilitado, SQL Server, Serilog, AutoMapper, FluentValidation, CORS abierto a cualquier origen ni el target `net6.0`. Cada uno contradice una decisión vigente o un requirement de `platform-api`, o merece un cambio propio.

### Fuera de alcance

- Persistencia, EF Core y migraciones (llegan con `add-tenancy-and-isolation`, ya con la estructura nueva).
- OpenAPI/Swagger y el cliente TypeScript generado (cambio propio).
- Autenticación, autorización y políticas por permiso (`add-identity-and-roles`).
- Crear el proyecto del frontend en `01.Client` (`add-admin-panel-shell`).
- SignalR (llega con eventos en vivo).
- Cambios de comportamiento de los endpoints de salud, del formato de errores, de la correlación o de la telemetría.

## Capabilities

### New Capabilities

(ninguna)

### Modified Capabilities

- `platform-api`: se precisa el versionado (versión no soportada) y el aislamiento entre módulos (reglas de capas y uso exclusivo de contratos desde la API).

## Impact

- **Código**: se mueven y renombran todos los proyectos de `src/` y `tests/` (con `git mv` para conservar el historial), cambian namespaces y nombres de ensamblado, se reescribe `Nexgate.slnx`, se elimina `src/Modules/Directory.Build.props`, se reemplazan los endpoints Minimal API del módulo `Sample` por un controller y se elimina el filtro de validación propio.
- **Dependencias**: se agrega el paquete de versionado de API para MVC. Se quita la referencia a `Microsoft.AspNetCore.App` de `06.Common`.
- **Contrato HTTP**: sin cambios para los clientes. Las rutas (`/api/v1/sample/...`, `/health/live`, `/health/ready`), los códigos de estado y el formato problem details se mantienen; lo confirman las mismas pruebas de integración.
- **Herramientas y documentación**: cambian las rutas de `dotnet run` (`src/02.Api/Nexgate.Api.Rest`), los perfiles de lanzamiento y la documentación que nombra rutas de proyectos.
- **Seguridad, privacidad y multitenancy**: no hay datos de negocio todavía y no cambia ningún comportamiento observable. Se preservan explícitamente las garantías actuales: los errores no exponen detalles internos (a diferencia del manejador de errores de SMT), los logs no registran datos personales y la organización activa sigue en logs y trazas. La regla de que los controllers solo usen contratos evita que la API se salte la capa de servicio, donde vivirán las verificaciones de organización y RLS.
- **Decisiones relacionadas**: implementa D-32 y D-33 (nuevas); mantiene D-27 (monolito modular, un proyecto por módulo y capa, comunicación por contratos) y D-28.
