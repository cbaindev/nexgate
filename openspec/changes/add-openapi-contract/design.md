# Design

## Context

La API v1 tiene hoy los endpoints de Identity (`auth`, `me`, `invitations`, `members`, administrador de plataforma) y de Tenancy (`platform/organizations`, `organization`, `sites`), todos como minimal APIs bajo `/api/v1`. Devuelven `IResult` sin tipo (`Results.Ok`, `Results.Problem`), así que un generador OpenAPI no sabe qué devuelve cada uno. Los errores salen con `ProblemDetails` enriquecido por `ProblemDetailsSetup` con `code` y `correlationId`. Los filtros `RequireAuthenticatedUser`, `RequirePermission` y `RequirePlatformOperator` de `BuildingBlocks/Access` responden 401 y 403.

El frontend (D-29) va a consumir la API a través de un cliente o proxy generado desde OpenAPI (ver `proposal.md`). Ese consumo todavía no existe; este cambio deja el contrato listo.

## Goals / Non-Goals

**Goals**
- Un documento OpenAPI 3.1 fiel a la API, con todas las respuestas, del que se pueda generar un cliente TypeScript tipado sin retoques.
- Que un cambio en la API que altere el contrato no pase inadvertido.
- Probar la API desde el navegador en desarrollo, con tokens reales.

**Non-Goals**
- Generar el cliente del frontend (cambio 7).
- Servir el documento fuera de `Development`.
- Pasar todos los endpoints a `TypedResults` (ver decisión 2).

## Decisions

### 1. Generador oficial de .NET y Swagger UI solo como interfaz

- El documento lo genera `Microsoft.AspNetCore.OpenApi` (`AddOpenApi`, `MapOpenApi`), el generador incluido en .NET 10, que produce OpenAPI 3.1 y entiende nulabilidad, `required` y los metadatos de minimal APIs.
- La interfaz es `Swashbuckle.AspNetCore.SwaggerUI` apuntando a `/openapi/v1.json`. Es solo HTML y JavaScript estático; no se usa el generador de Swashbuckle.
- *Alternativa descartada:* Swashbuckle completo (generador más interfaz). Duplica lo que .NET 10 ya trae y su soporte de 3.1 llega después. *Alternativa descartada:* NSwag, que además genera clientes; la generación del cliente se decide con el frontend y no conviene atarla al backend.
- *Alternativa descartada:* Scalar como interfaz. Es más moderna, pero se pidió Swagger UI y cambiarla después no toca el contrato.

### 2. Respuestas declaradas con metadatos, no con `TypedResults`

- Cada endpoint declara `WithName` (identificador de operación), `WithSummary`, `WithTags` (módulo), `Produces<T>(estado)` para el éxito y `ProducesProblem`/`ProducesValidationProblem` para los errores que su servicio puede devolver.
- Los filtros de acceso agregan por sí solos sus metadatos: `RequireAuthenticatedUser` declara 401 y el requisito de token; `RequirePermission` y `RequirePlatformOperator` además declaran 403. El límite de tasa declara 429 en los dos endpoints que lo usan. Así esas respuestas no se repiten ni se olvidan en cada endpoint.
- *Alternativa descartada:* reescribir los endpoints con `Results<Ok<T>, ProblemHttpResult, ...>`. Da garantía en compilación, pero obliga a reescribir los helpers `ToHttp` de los dos módulos y las uniones de seis o siete tipos son difíciles de leer. Las pruebas de integración existentes ya ejercitan cada respuesta; la decisión 5 agrega la verificación de que lo declarado coincide con lo observado en esas pruebas.

### 3. Esquemas de error y de seguridad

- Un transformador del documento define un esquema `ProblemDetails` con `type`, `title`, `status`, `detail`, `instance`, `code` (obligatorio) y `correlationId` (obligatorio), y `ValidationProblemDetails` que agrega `errors` (mapa de campo a mensajes). Todas las respuestas de error referencian uno de los dos.
- El esquema de seguridad es `bearer` HTTP con formato JWT. Un transformador de operación lo aplica a las operaciones cuyos metadatos dicen que exigen token (decisión 2), no a todas.
- Las operaciones del operador de plataforma llevan la etiqueta `Platform`, separadas de las de la organización.

### 3b. Números estrictos en JSON

- *Agregado durante la implementación.* Con las opciones web por defecto, .NET acepta números escritos como texto (`"5"`) y el documento los declara como "entero o texto con patrón", lo que da tipos confusos en el cliente generado. La API pasa a `JsonNumberHandling.Strict`: los números son números en el contrato y en la API.
- *Alternativa descartada:* corregir solo el esquema con un transformador. El documento diría "entero" mientras la API sigue aceptando texto: contrato y comportamiento no coincidirían.

### 4. Nombres de operación y estabilidad

- Identificadores en camelCase inglés con verbo y recurso: `signIn`, `refreshToken`, `signOut`, `getCurrentUser`, `getMyPermissions`, `createInvitation`, `acceptInvitation`, `listMembers`, `removeMember`, `assignRole`, `revokeRole`, `createOrganization`, `reissueAdministratorInvitation`, `getOrganization`, `listSites`, `getSite`, `createSite`, `updateSite`. Son los nombres de los métodos del cliente generado; cambiar uno es un cambio de contrato.
- El documento se escribe con propiedades y rutas en orden determinista (el generador ordena rutas; un transformador ordena esquemas y respuestas) para que dos generaciones sean idénticas byte a byte.
- `servers` no se incluye en el archivo versionado: el cliente fija la URL base por configuración o la resuelve el proxy.

### 5. Contrato versionado y su prueba

- El archivo vive en `api/nexgate-v1.openapi.json`, fuera de `src`, porque lo consumen el backend y el frontend.
- `OpenApiContractTests` levanta la API con `WebApplicationFactory` en `Development`, pide `/openapi/v1.json`, normaliza fin de línea y compara con el archivo. Si difiere, falla con un resumen de rutas y esquemas agregados, quitados o cambiados.
- Para actualizarlo: `NEXGATE_UPDATE_OPENAPI=1 dotnet test --filter OpenApiContract` reescribe el archivo, y el diff se revisa con el cambio. Queda en el README.
- Otra prueba recorre el documento y exige: identificador único por operación, resumen, al menos una respuesta de éxito, esquema de error en toda respuesta 4xx y 5xx, 401 en toda operación con requisito de token, y que ninguna ruta de salud aparezca.
- Coincidencia con la API real: las pruebas de integración pasan por un `DelegatingHandler` del `HttpClient` de pruebas que registra (método, plantilla de ruta, estado) de cada respuesta; al terminar la colección, una prueba verifica que cada estado observado esté declarado en el documento para esa operación.
- *Ajustado durante la implementación.* En lugar de un `DelegatingHandler` y una prueba final (xUnit no garantiza el orden), un `IStartupFilter` de la fábrica de pruebas compara cada respuesta de un endpoint `/api/v1` con los metadatos de respuesta de ese endpoint, que son la fuente del documento. Las no declaradas se acumulan y la fábrica lanza al liberarse, así falla la prueba que produjo la respuesta. Se comprobó quitando el 404 de `getSite`: fallaron la comparación del contrato y las pruebas que piden un sitio fuera de alcance. *Alternativa descartada:* confiar solo en la revisión del diff; un `ProducesProblem` olvidado no se ve en el diff porque justamente falta.
- *Alternativa descartada:* generar el archivo al compilar con `Microsoft.Extensions.ApiDescription.Server`. Arranca el host durante el build (clave de firma, cadena de conexión, configuración de entorno) y vuelve el build frágil; la prueba da lo mismo sin tocar el build.

### 6. Solo en `Development`

- `MapOpenApi` y `UseSwaggerUI` se registran solo si el entorno es `Development`. En cualquier otro entorno las rutas no existen y responden 404 por el pipeline normal.
- Swagger UI se configura sin `persistAuthorization` (valor por defecto: apagado) y sin credenciales precargadas.
- *Alternativa descartada:* una opción de configuración para habilitarlo en cualquier entorno. Se agrega si un entorno de pruebas compartido lo necesita, con su propio cambio.

## Cómo se prueba

- **Contrato:** `OpenApiContractTests` compara el documento generado con el versionado y verifica las reglas de la decisión 5.
- **Coincidencia con la API:** el registro de respuestas observadas en las pruebas de integración contra PostgreSQL real.
- **Entorno:** prueba de integración que pide `/swagger` y `/openapi/v1.json` en `Development` (200) y en `Production` (404).
- **Manual:** abrir `/swagger`, iniciar sesión con `signIn`, autorizar con el token y llamar `listSites`; verificar un 403 con un usuario sin permiso.

## Risks / Trade-offs

- **Metadatos que se desvían del código.** Mitigado con la verificación de respuestas observadas; un estado que ninguna prueba ejercita puede quedar sin declarar. Las pruebas actuales cubren los escenarios de los specs, que incluyen los errores.
- **Ruido en el diff.** Una actualización del generador puede reordenar o reformatear el documento. Se acepta: se regenera y se revisa como cualquier cambio de dependencia.
- **Superficie expuesta.** El documento describe todos los endpoints. Solo se sirve en `Development` y no revela nada que la API misma no revele.

## Migration Plan

- No hay clientes existentes. El archivo se crea con este cambio.
- Rollback: revertir la rama; no hay cambios de base de datos ni de configuración obligatoria.

## Open Questions

- ¿Qué es exactamente el proxy del frontend: un cliente TypeScript generado (por ejemplo `openapi-typescript` u Orval sobre TanStack Query) o un servidor intermedio (BFF) que se arma desde el documento? No cambia este diseño, pero si es un BFF, el documento se convierte también en el contrato de ese servidor y conviene decidir dónde se guarda el token de renovación (riesgo ya anotado en `add-identity-and-roles`). Se decide con `add-admin-panel-shell`.
