# Tasks

## 1. Estructura de directorios y proyectos (sin cambios de comportamiento)

- [x] 1.1 Mover con `git mv` los proyectos a la estructura numerada de `design.md` (D1): `Nexgate.Host` → `src/02.Api/Nexgate.Api.Rest`, `Nexgate.Workers` → `src/02.Api/Nexgate.Api.Workers`, `BuildingBlocks` → `src/06.Common/Nexgate.Common.BuildingBlocks`, `Sample.Contracts` → `src/03.Service/Nexgate.Service.Sample.Contracts`, `Sample.Application` → `src/03.Service/Nexgate.Service.Sample`, `Sample.Domain` → `src/05.Domain/Nexgate.Domain.Sample`, `tests/*` → `src/07.Test/Nexgate.Test.Architecture` y `src/07.Test/Nexgate.Test.Api`; verificar con `git status` que todo figura como renombrado
- [x] 1.2 Mover el contenido de `Sample.Infrastructure` (registro DI y endpoints) a `Nexgate.Service.Sample` y a `Nexgate.Api.Rest` respectivamente, todavía con Minimal API, y eliminar el proyecto; verificar que `dotnet build` compila
- [x] 1.3 Renombrar los `.csproj`, actualizar `ProjectReference`, namespaces y `using`, eliminar `src/Modules/Directory.Build.props` y reescribir `Nexgate.slnx` con carpetas `01.Client` … `07.Test`; verificar `dotnet build` sin warnings
- [x] 1.4 Ajustar `ProjectGraph` solo lo necesario para que las reglas actuales sigan aplicando con la nueva deducción de módulo y capa (carpeta + nombre `Nexgate.<Capa>.<Modulo>[.Contracts]`); verificar `dotnet test` en verde con las mismas aserciones de integración
- [x] 1.5 Buscar rutas y nombres viejos (`Nexgate.Host`, `Nexgate.Workers`, `src/Modules`, `tests/`, `BuildingBlocks`) en `README.md`, `launchSettings.json`, `infra/` y scripts, y actualizarlos; verificar con `git grep` sin resultados fuera de `openspec/changes/archive` y siguiendo el README para levantar la API con `dotnet run --project src/02.Api/Nexgate.Api.Rest`

## 2. División de BuildingBlocks

- [x] 2.1 Mover las piezas HTTP (middlewares de correlación y telemetría, problem details, manejador global de errores, mapeo de health checks, `HttpContextCurrentOrganization`) a `Nexgate.Api.Rest/Middlewares` y `Extensions` según la tabla de D6; verificar `dotnet build`
- [x] 2.2 Quitar `Microsoft.AspNetCore.App` de `Nexgate.Common.BuildingBlocks`, reemplazándolo por los paquetes `Microsoft.Extensions.*` necesarios, y agregar la instrumentación de ASP.NET Core a la telemetría desde `Api.Rest`; verificar `dotnet build` y que `TelemetryTests` y `CorrelationTests` pasan sin cambios en sus aserciones
- [x] 2.3 Verificar que `Nexgate.Api.Workers` arranca con `dotnet run --project src/02.Api/Nexgate.Api.Workers` y emite logs JSON sin referenciar el framework web

## 3. Controllers en lugar de Minimal API

- [x] 3.1 Agregar `Asp.Versioning.Mvc`, registrar `AddControllers` y el versionado (`DefaultApiVersion` 1.0, sin versión por defecto implícita, `ReportApiVersions`, versión no soportada → 404) y `MapControllers`; verificar `dotnet build`
- [x] 3.2 Crear `Controllers/Sample/SampleController` con `[ApiController]` y las acciones `ping`, `greet` y `fail` (esta última devuelve 404 si `Sample:EnableFailureEndpoint` está apagado) y eliminar `MapSampleEndpoints` y el grupo `/api/v1`; verificar con `VersioningTests` y `ErrorTests` sin cambiar sus aserciones
- [x] 3.3 Mover el modelo de entrada a `Nexgate.Service.Sample.Contracts` como `GreetRequest`, eliminar `ValidationFilter` y comprobar que el 400 automático incluye `code = validation_error`, `correlationId` y la clave `Name` en `errors` (si no, aplicar el plan B de D5); verificar con `Invalid_request_returns_400_listing_each_invalid_field` y `Missing_required_field_returns_400`
- [x] 3.4 Agregar pruebas de integración para la versión no soportada (`/api/v9/sample/ping` → 404 problem details con `code = not_found`) y para `fail` con la bandera apagada (404); verificar con `dotnet test`
- [x] 3.5 Hacer `internal` las implementaciones de `Nexgate.Service.Sample` y exponer su registro DI con `AddSampleService()`, invocado desde `Api.Rest/Extensions/ModuleExtensions`; verificar `dotnet build` y `dotnet test`

## 4. Reglas de arquitectura para la nueva estructura

- [x] 4.1 Reescribir `ArchitectureRules` con la tabla de dependencias de D2 (entre módulos solo `Service.<M>.Contracts`; `Domain` y `Contracts` sin referencias, paquetes ni frameworks; `Data` sin `Service`; `Common` sin módulos, sin `02.Api` y sin `Microsoft.AspNetCore.App`; módulos sin `02.Api`) y excluir `07.Test`; verificar con pruebas sobre grafos sintéticos, una por regla, que fallan ante la referencia prohibida
- [x] 4.2 Actualizar la prueba de descubrimiento para exigir `Nexgate.Api.Rest`, `Nexgate.Api.Workers`, `Nexgate.Common.BuildingBlocks` y las capas `Contracts`, `Service` y `Domain` de `Sample`; verificar con `dotnet test` y comprobando que falla si se renombra una carpeta
- [x] 4.3 Agregar la prueba por reflexión de D3 (los controllers solo usan tipos de `*.Contracts`, `Nexgate.Common.*` o del framework en constructores, parámetros y retornos declarados); verificar con `dotnet test` y con un controller de prueba que use un tipo interno y haga fallar la regla

## 5. Documentación y cierre

- [x] 5.1 Agregar D-32 (estructura en capas numeradas al estilo SMT con un proyecto por módulo y capa, sentido `Api → Service → Data → Domain`) y D-33 (API REST con controllers y versionado por segmento de URL) a `docs/01-decisiones.md`; verificar revisando el diff
- [x] 5.2 Actualizar `docs/05-arquitectura-tecnica.md` (estructura de cada módulo y del repositorio, sección de pruebas), `docs/08-plan-de-cambios-openspec.md` (agregar este cambio al plan), `README.md`, `CLAUDE.md` y el contexto de `openspec/config.yaml` donde nombran rutas o capas; verificar con `git grep -n "src/Modules\|Application\|Infrastructure" docs README.md CLAUDE.md openspec/config.yaml` sin referencias a la estructura vieja
- [x] 5.3 Build y pruebas desde cero (`git clean -xdf` de `bin/obj`, `dotnet build`, `dotnet test` con PostgreSQL de `docker compose`); verificar ambos en verde
- [x] 5.4 Validar el cambio con `openspec validate adopt-smt-layout-and-controllers --strict`; verificar que no reporta errores

## Workflow follow-up

- Archivar el cambio con `openspec archive adopt-smt-layout-and-controllers`, para que el delta de `platform-api` pase a `openspec/specs/`. (Hecho el 2026-11-11.)
