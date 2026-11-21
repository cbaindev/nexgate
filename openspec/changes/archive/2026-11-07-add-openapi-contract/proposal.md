# Proposal

## Why

El frontend (D-29, React + TypeScript) se va a comunicar con el backend a través de un proxy o cliente generado a partir de una descripción OpenAPI de la API. Hoy la API no publica ninguna descripción y sus endpoints no declaran qué devuelven, así que no hay de dónde generar ese cliente ni forma de probar la API desde el navegador.

Si el documento OpenAPI va a ser la base del cliente del frontend, tiene que ser un **contrato**: completo (todas las respuestas, incluidos los errores), estable entre ejecuciones y revisado en cada cambio. Un documento que se genera solo en tiempo de ejecución y nadie mira se desvía de la API sin que nadie se entere, y el cliente generado se rompe en el frontend en vez de en el backend.

## What Changes

- **Descripción OpenAPI de la API v1** generada a partir de los endpoints, con:
  - un identificador de operación estable y único por endpoint, para que el cliente generado tenga nombres predecibles;
  - resumen y descripción en español;
  - los esquemas de cada cuerpo de solicitud y de respuesta;
  - todas las respuestas de error posibles de cada operación, con el formato estándar de errores (código, título, estado, correlación y campos inválidos);
  - el esquema de autenticación por token y qué operaciones lo exigen.
- **Documento versionado en el repositorio** (`api/nexgate-v1.openapi.json`). Una prueba genera el documento y falla si difiere del versionado, así que todo cambio en la API se ve en el diff y el frontend genera su cliente desde ese archivo, sin levantar el backend.
- **Swagger UI** en `/swagger`, solo en `Development`, con el botón de autorización para pegar el token de acceso y probar los endpoints protegidos. El documento también se sirve en `/openapi/v1.json` solo en `Development`.
- Registrar la decisión **D-32**: el contrato entre el frontend y el backend es el documento OpenAPI versionado y el cliente del frontend se genera desde él.

Fuera de alcance:
- **El frontend y la generación del cliente o proxy:** llegan con `add-admin-panel-shell` (cambio 7), que elige la herramienta de generación.
- **Publicar el documento o Swagger UI fuera de `Development`.** Si un entorno de pruebas lo necesita, se habilita en un cambio posterior.
- **Descripción OpenAPI de la comunicación con dispositivos** (MQTT): no es HTTP y se describe con `add-device-sync-protocol`.
- **Endpoints de salud:** no son de negocio y no aparecen en el documento.
- **Versionado semántico del documento y política de cambios incompatibles:** mientras haya un solo cliente se revisa en el diff; se define antes de la PWA de residentes.

## Capabilities

### New Capabilities

Ninguna.

### Modified Capabilities

- `platform-api`: agrega la descripción OpenAPI como contrato versionado de la API y la referencia interactiva en desarrollo.

## Impact

- **Código modificado:** los endpoints de Identity y Tenancy declaran nombre de operación, resumen, respuestas y errores. Los filtros de acceso de `BuildingBlocks` declaran por sí solos el requisito de token y las respuestas 401 y 403, así que cada endpoint no los repite. Transformadores del documento para el esquema de errores y el de seguridad.
- **Código nuevo:** registro de OpenAPI y Swagger UI en `BuildingBlocks` o en el host, la prueba de contrato y el archivo `api/nexgate-v1.openapi.json`.
- **Dependencias:** `Microsoft.AspNetCore.OpenApi` (generador oficial de .NET 10) y `Swashbuckle.AspNetCore.SwaggerUI` (solo la interfaz, sin el generador de Swashbuckle).
- **Base de datos:** sin cambios.
- **Multitenancy:** sin cambios. Swagger UI llama a la misma API con el token del usuario, así que pasa por la misma autenticación, membresía, permisos y RLS que cualquier cliente.
- **Seguridad:**
  - El documento describe la superficie de la API. Fuera de `Development` no se sirve ni el documento ni Swagger UI (404); el archivo versionado no tiene secretos.
  - Swagger UI no guarda el token entre recargas.
  - Los ejemplos del documento no llevan tokens, claves ni correos reales.
- **Privacidad:** el documento describe esquemas, no datos. Los ejemplos usan correos de dominios reservados (`example.com`).
- **Decisiones relacionadas:** agrega D-32; aplica D-29 y D-05 (la interfaz se arma con lo que devuelve la API).
- **Dependencia de cambios:** se implementa sobre `add-identity-and-roles`, que define la mayoría de los endpoints.
