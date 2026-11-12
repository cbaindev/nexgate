# Design

## Context

La motivación está en `proposal.md`. Estado actual relevante:

- `Nexgate.slnx` con `src/Nexgate.Host`, `src/Nexgate.Workers`, `src/BuildingBlocks` y `src/Modules/Sample/Sample.{Contracts,Domain,Application,Infrastructure}`; pruebas en `tests/`.
- `src/Modules/Directory.Build.props` fuerza `AssemblyName` y `RootNamespace` a `Nexgate.Modules.<Proyecto>`.
- Los endpoints de negocio se registran con Minimal API: `Program.cs` crea el grupo `/api/v1` y `SampleModule.MapSampleEndpoints` agrega `/sample/ping`, `/sample/greet` (con el filtro `WithValidation<T>`) y `/sample/fail` (solo si `Sample:EnableFailureEndpoint`).
- `BuildingBlocks` mezcla piezas sin HTTP (`CorrelationId`, `ICurrentOrganization`, `LogScope`, `TelemetryExtensions`, `DatabaseHealthCheck`) con piezas de ASP.NET Core (middlewares de correlación y de telemetría, problem details, manejador global de errores, mapeo de health checks, `HttpContextCurrentOrganization`, filtro de validación).
- Las pruebas de arquitectura leen los `.csproj` de `src/` y deducen módulo y capa del patrón `src/Modules/<Modulo>/<Modulo>.<Capa>`.
- Referencia de SMT: `dev/ShiftManagment` (capas numeradas, `Api.Rest` con `Controllers/`, `Middlewares/`, `Extensions/`; `Service.Discovery` → `Data.Db` → `Domain.Models`; `Common.Helpers`).

## Goals / Non-Goals

**Goals:**

- Que alguien que conoce SMT encuentre cada cosa en el mismo lugar en Nexgate.
- Conservar el aislamiento entre módulos verificado en build (D-27).
- Migrar sin cambios observables: las pruebas de integración existentes pasan solo con cambios de namespace y de rutas de proyecto.

**Non-Goals:**

- Traer código de SMT. Solo se adopta la forma.
- Resolver la persistencia: `04.Data` queda definido por convención pero vacío.

## Decisions

### D1. Capas numeradas de SMT con un proyecto por módulo dentro de cada capa

Estructura final:

```
src/
  01.Client/                              reservado (frontend React, add-admin-panel-shell)
  02.Api/
    Nexgate.Api.Rest/                     antes Nexgate.Host
      Controllers/<Modulo>/
      Middlewares/  Extensions/  Program.cs
    Nexgate.Api.Workers/                  antes Nexgate.Workers
  03.Service/
    Nexgate.Service.<Modulo>.Contracts/   interfaces públicas, DTOs y modelos de entrada (antes <Modulo>.Contracts)
    Nexgate.Service.<Modulo>/             servicios de aplicación y registro DI (antes <Modulo>.Application)
  04.Data/
    Nexgate.Data.<Modulo>/                EF Core, repositorios, migraciones del schema del módulo
  05.Domain/
    Nexgate.Domain.<Modulo>/              entidades y reglas puras (antes <Modulo>.Domain)
  06.Common/
    Nexgate.Common.BuildingBlocks/        transversal sin HTTP (antes BuildingBlocks)
  07.Test/
    Nexgate.Test.Architecture/            antes tests/Nexgate.ArchitectureTests
    Nexgate.Test.Api/                     antes tests/Nexgate.Host.Tests
```

`Sample.Infrastructure` desaparece: su registro DI pasa a `Nexgate.Service.Sample` y sus endpoints a un controller. `Nexgate.slnx` usa carpetas de solución con los mismos nombres numerados. Se elimina `src/Modules/Directory.Build.props`: el nombre del proyecto ya es el nombre del ensamblado y el namespace raíz.

- **Alternativa descartada: un único proyecto por capa, como SMT** (`Nexgate.Service`, `Nexgate.Data.Db`, `Nexgate.Domain.Models`, con carpetas por módulo). Es la copia fiel, pero el aislamiento entre módulos pasaría a depender de namespaces y revisión de código, no del compilador: rompe D-27 ("para poder extraerlo como microservicio") y la regla de `CLAUDE.md` de referenciar módulos solo por `*.Contracts`.
- **Alternativa descartada: mantener `src/Modules/<Modulo>/` y solo pasar a controllers.** No cumple el objetivo de alinear la navegación con SMT.

### D2. Sentido de dependencias `Api → Service → Data → Domain`

Se adopta el sentido de SMT: el servicio usa los repositorios de su módulo y la capa de datos conoce el dominio. Reglas que verifican las pruebas de arquitectura (leyendo los `.csproj`):

| Proyecto | Puede referenciar |
|---|---|
| `Nexgate.Domain.<M>` | nada (sin proyectos, paquetes ni frameworks) |
| `Nexgate.Service.<M>.Contracts` | nada (sin proyectos, paquetes ni frameworks) |
| `Nexgate.Data.<M>` | `Domain.<M>`, `Common.*` |
| `Nexgate.Service.<M>` | `Service.<M>.Contracts`, `Domain.<M>`, `Data.<M>`, `Service.<Otro>.Contracts`, `Common.*` |
| `Nexgate.Common.*` | otros `Common.*`; nunca módulos ni `02.Api`; sin `Microsoft.AspNetCore.App` |
| `Nexgate.Api.Rest` / `Nexgate.Api.Workers` | todo lo anterior (son la raíz de composición) |

Ningún proyecto de módulo referencia `02.Api`. Entre módulos solo se referencian `Service.<Otro>.Contracts`. El módulo y la capa se deducen de la carpeta (`03.Service`, `04.Data`, `05.Domain`) y del nombre (`Nexgate.<Capa>.<Modulo>[.Contracts]`). Una prueba de "descubrimiento" sigue fallando si la estructura cambia y las reglas quedan evaluándose en vacío.

- **Alternativa descartada: invertir la dependencia (`Data` implementa puertos definidos en `Service`)**, que es el sentido actual (`Infrastructure → Application`). Aísla mejor el servicio de EF Core, pero contradice la forma de SMT que se quiere adoptar. Como hoy no hay persistencia, el costo de elegir el sentido de SMT es nulo; si hiciera falta invertirlo para un módulo puntual, se discute en el cambio que lo necesite.

### D3. Controllers solo con contratos

`Nexgate.Api.Rest` referencia `Service.<M>` y `Data.<M>` solo para registrarlos en DI (`Extensions/ModuleExtensions.cs`, equivalente a `BusinessService.cs` de SMT, que llama a `AddSampleService()`, etc.). Como el `.csproj` no puede impedir que un controller use una clase interna de esas capas, se agrega una prueba de arquitectura por reflexión: los tipos de los parámetros de constructor y de las acciones (y sus tipos de retorno declarados) de cada controller deben pertenecer a un ensamblado `*.Contracts`, a `Nexgate.Common.*` o al framework (se exceptúa el propio controller, que aparece como argumento genérico en `ILogger<TController>`). Además, las clases de servicio de `Nexgate.Service.<M>` son `internal` y se exponen solo por su interfaz pública de `Contracts`.

- **Alternativa descartada: controllers dentro de cada módulo** (`Nexgate.Service.<M>/Controllers`, cargados como application parts). Mantiene todo el módulo junto, pero `03.Service` pasaría a depender de MVC y se aleja de SMT, donde todos los controllers están en `Api.Rest/Controllers`.

### D4. Controllers con `[ApiController]` y versionado por segmento de URL

- `builder.Services.AddControllers()` y `app.MapControllers()`.
- Versionado con `Asp.Versioning.Mvc` (sucesor de `Microsoft.AspNetCore.Mvc.Versioning`, que usa SMT): rutas `api/v{version:apiVersion}/<recurso>`, `DefaultApiVersion = 1.0`, `AssumeDefaultVersionWhenUnspecified = false` (una ruta sin versión no matchea y da 404, como hoy) y `ReportApiVersions = true`. Una versión no soportada se responde con 404 en formato problem details, para que todo lo que esté fuera de una versión válida se comporte igual. Con lectura por segmento de URL ese ya es el comportamiento de `Asp.Versioning` (verificado quitando la opción); igual se fija `UnsupportedApiVersionStatusCode = 404` para que la intención quede explícita.
- `SampleController` en `Controllers/Sample/` con `[Route("api/v{version:apiVersion}/sample")]`, acciones `ping`, `greet` y `fail`. La acción `fail` devuelve 404 salvo que `Sample:EnableFailureEndpoint` esté activo (hoy el endpoint directamente no se registra; el resultado observable es el mismo).
- **Alternativa descartada: prefijo fijo (`[Route("api/v1/...")]`) sin paquete.** Es más simple, pero cuando aparezca la v2 obliga a duplicar controllers o rutas; SMT ya resuelve esto con el paquete de versionado.

### D5. Validación con `[ApiController]` y el mismo formato de error

Se elimina `WithValidation<T>`. El modelo de entrada (`GreetRequest`, antes `EchoRequest`) pasa a `Nexgate.Service.Sample.Contracts` con DataAnnotations (que forman parte del runtime, no son un paquete externo). En un record, los atributos van sobre el parámetro del constructor: MVC lanza una excepción si están en la propiedad (`[property: ...]`, como los tenía la Minimal API). `[ApiController]` responde 400 automáticamente con `ValidationProblemDetails`; como su título genérico viene en inglés, la personalización de problem details lo reemplaza por "Error de validación". Como deriva de `HttpValidationProblemDetails` y la fábrica de problem details de MVC aplica `ProblemDetailsOptions.CustomizeProblemDetails`, se siguen agregando `code = "validation_error"` y `correlationId`. Si en la verificación no fuera así, se configura `ApiBehaviorOptions.InvalidModelStateResponseFactory` para escribir con `IProblemDetailsService`.

- **Alternativa descartada: FluentValidation, como SMT.** Agrega una dependencia y su integración automática con ASP.NET Core está discontinuada. Se puede evaluar en un cambio propio cuando haya validaciones de negocio complejas.

### D6. División de BuildingBlocks: lo HTTP va a `Nexgate.Api.Rest`

| Pieza actual | Destino |
|---|---|
| `CorrelationId` (constantes, validación y generación) | `06.Common/Nexgate.Common.BuildingBlocks/Correlation` |
| `ICurrentOrganization`, `NullCurrentOrganization`, `OrganizationContextKeys` | `Nexgate.Common.BuildingBlocks/Tenancy` |
| `LogScope`, `TelemetryExtensions` | `Nexgate.Common.BuildingBlocks/Telemetry` (la instrumentación de ASP.NET Core se agrega desde `Api.Rest` mediante un parámetro de configuración de trazas) |
| `DatabaseHealthCheck` | `Nexgate.Common.BuildingBlocks/Health` |
| `CorrelationIdMiddleware`, `TelemetryEnrichmentMiddleware`, `GlobalExceptionHandler` | `02.Api/Nexgate.Api.Rest/Middlewares` |
| `ProblemDetailsSetup` (pasa a llamarse `WebDefaultsExtensions`), `HealthEndpoints` | `Nexgate.Api.Rest/Extensions` |
| `HttpContextCurrentOrganization` | `Nexgate.Api.Rest/Tenancy` |
| `ValidationFilter` | `Nexgate.Api.Rest/Filters` mientras siga la Minimal API; se elimina en el paso a controllers (D5) |

Al quitar `Microsoft.AspNetCore.App` de `Common`, `Nexgate.Api.Workers` declara `Microsoft.Extensions.Hosting`, que antes recibía de forma transitiva.

Así `06.Common` no depende de ASP.NET Core y `Api.Workers` no arrastra el framework web, como en SMT, donde `Common.Helpers` no conoce HTTP y los middlewares viven en `Api.Rest`. Los health checks siguen siendo endpoints del middleware de health checks, no un controller: el spec solo exige el comportamiento, y el middleware ya lo cumple sin código propio.

- **Alternativa descartada: dejar todo en `Common.BuildingBlocks`.** Menos movimientos, pero mantiene la dependencia de ASP.NET Core en una capa común que también usan los workers y los futuros proyectos de datos.

### D7. Pruebas en `src/07.Test`

Se ubican dentro de `src/` con número propio para que toda la solución siga la misma convención. `ProjectGraph` excluye `07.Test` de las reglas de capas. `WebApplicationFactory<Program>` sigue funcionando porque `Program` conserva `public partial class Program`.

- **Alternativa descartada: mantener `tests/` en la raíz.** Es lo habitual en .NET, pero queda como la única carpeta fuera de la convención numerada.

### D8. Movimientos con historial y en pasos verificables

Se usa `git mv` para cada carpeta y archivo, y se separa el trabajo en commits que compilan y pasan las pruebas: (1) mover y renombrar sin tocar comportamiento, (2) dividir BuildingBlocks, (3) pasar a controllers, (4) reglas de arquitectura nuevas, (5) documentación.

## Cómo se prueba

- **Integración (PostgreSQL real, como hoy)**: las suites de `Nexgate.Test.Api` (salud, errores, versionado, correlación, telemetría) se mantienen sin cambiar sus aserciones; solo cambian namespaces y referencias. Se agregan casos para la versión no soportada (`/api/v9/sample/ping` → 404 problem details) y para que `fail` dé 404 con la bandera apagada.
- **Arquitectura**: pruebas sobre el grafo real de la solución y pruebas con grafos sintéticos por cada regla de D2 (incluyendo `Data → Service` y `Common → AspNetCore` rechazados), más la prueba por reflexión de D3 con un controller de prueba que usa un tipo interno.
- **Build**: `dotnet build` sin warnings (`TreatWarningsAsErrors`) y `dotnet test` en verde después de cada grupo de tareas.

## Risks / Trade-offs

- [El 400 automático de MVC no incluye `code` o `correlationId`, o nombra distinto los campos] → la prueba `Invalid_request_returns_400_listing_each_invalid_field` lo detecta; plan B en D5 (`InvalidModelStateResponseFactory`).
- [`Asp.Versioning` responde 400 en lugar de 404 ante una versión no soportada, o la ruta sin versión pasa a 400] → se fija `UnsupportedApiVersionStatusCode` y lo cubren las pruebas de versionado.
- [Las reglas de arquitectura quedan evaluándose en vacío por un error en la deducción de capas] → la prueba de descubrimiento exige encontrar `Api.Rest`, `Api.Workers`, `Common.BuildingBlocks` y las capas de `Sample`.
- [Rutas de proyecto rotas en scripts, `launchSettings.json`, `README.md` o perfiles de Visual Studio] → tarea explícita de búsqueda de rutas viejas (`Nexgate.Host`, `src/Modules`, `tests/`) en todo el repositorio.
- [Un solo proyecto `Api.Rest` con todos los controllers crece mucho] → se organiza por carpeta de módulo (`Controllers/<Modulo>/`); si un módulo se extrae como servicio, se llevan sus controllers con él.
- [Trade-off: el sentido `Service → Data` acopla los servicios a la capa de datos] → aceptado por coherencia con SMT (D2); las reglas de servicio que necesiten pruebas puras viven en `Domain`.

## Migration Plan

No hay despliegues ni datos: el cambio es local al repositorio. Rollback: revertir los commits del cambio. Los clientes HTTP no notan la diferencia porque las rutas y respuestas no cambian.
