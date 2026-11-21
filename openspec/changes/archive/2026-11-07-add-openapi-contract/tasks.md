# Tasks

## 1. Documento OpenAPI y Swagger UI

- [x] 1.1 Agregar `Microsoft.AspNetCore.OpenApi` y `Swashbuckle.AspNetCore.SwaggerUI`, registrar `AddOpenApi("v1")` y, solo en `Development`, `MapOpenApi` y Swagger UI en `/swagger` (sin persistir la autorización); excluir los endpoints de salud; verificar con una prueba de integración que `/swagger` y `/openapi/v1.json` responden 200 en `Development` y 404 en `Production`
- [x] 1.2 Transformadores del documento: esquemas `ProblemDetails` y `ValidationProblemDetails` con `code` y `correlationId` obligatorios, esquema de seguridad `bearer` JWT aplicado solo a operaciones que exigen token, y orden determinista de esquemas y respuestas; verificar con una prueba que dos generaciones son idénticas
- [x] 1.3 Hacer que `RequireAuthenticatedUser`, `RequirePermission` y `RequirePlatformOperator` declaren sus metadatos (requisito de token, 401 y 403) y que el límite de tasa declare 429; verificar con una prueba que lee el documento

## 2. Metadatos de los endpoints

- [x] 2.1 Endpoints de Identity: `WithName` según la decisión 4 del design, `WithSummary` en español, `WithTags`, `Produces<T>` y los errores de cada servicio; verificar leyendo el documento en Swagger UI
- [x] 2.2 Endpoints de Tenancy con el mismo criterio, separando `Platform` de `Organization` y `Sites`; verificar leyendo el documento en Swagger UI

## 3. Contrato versionado y pruebas

- [x] 3.1 Crear `api/nexgate-v1.openapi.json` y `OpenApiContractTests`: comparación con el archivo, actualización con `NEXGATE_UPDATE_OPENAPI=1` y reglas de completitud (identificador único, resumen, respuesta de éxito, esquema de error en 4xx y 5xx, 401 con token, sin salud); verificar con `dotnet test` y comprobando que quitar un `ProducesProblem` la hace fallar
- [x] 3.2 Registro de respuestas observadas en las pruebas de integración y prueba de que cada estado observado está declarado en el documento para su operación; verificar con `dotnet test`
- [x] 3.3 Documentación: README (Swagger UI, cómo autorizar con el token, cómo regenerar el contrato), `docs/05-arquitectura-tecnica.md` (contrato OpenAPI y cliente generado), D-32 en `docs/01-decisiones.md` y el cambio en la tabla de `docs/08`; verificar abriendo `/swagger`, iniciando sesión con `signIn`, autorizando y llamando `listSites`, y con un usuario sin permiso recibiendo 403
- [x] 3.4 Ejecutar build limpio y todos los tests; verificar `dotnet build` y `dotnet test` verdes

## Notas de implementación

- La verificación de respuestas observadas usa un `IStartupFilter` en la fábrica de pruebas y falla al liberar la fábrica, en vez de un `DelegatingHandler` y una prueba final (design, decisión 5).
- Se agregó `JsonNumberHandling.Strict` para que el contrato declare enteros y la API los exija (design, decisión 3b).
- Los endpoints de salud no son minimal APIs con descripción, y además el documento incluye solo rutas bajo `/api/v1/`.
- Verificación final: build limpio sin warnings y 222 pruebas en verde (10 de arquitectura y 212 del host). Quitar el 404 declarado de `getSite` hizo fallar la comparación del contrato y cuatro pruebas por respuesta no declarada. Swagger UI abierto en el navegador con las 18 operaciones agrupadas por etiqueta; una llamada a `listSites` sin token desde la página responde 401 `authentication_required`. El flujo con token (`signIn`, autorizar, `listSites` y 403 con un usuario sin permiso) lo cubre la prueba de integración `Protected_operation_called_as_from_the_reference_goes_through_permissions`; la prueba manual con credenciales queda para el usuario.
