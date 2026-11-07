# Tasks

## 1. Solución y estructura base

- [x] 1.1 Crear `global.json`, `Directory.Build.props`, `.editorconfig` y `.gitignore`; verificar con `dotnet --version` y `dotnet build` sin warnings
- [x] 1.2 Crear la solución con `Nexgate.Host`, `Nexgate.Workers` y `BuildingBlocks`; verificar que `dotnet build` compila toda la solución
- [x] 1.3 Crear el módulo de ejemplo `Sample` con sus cuatro proyectos (Contracts, Domain, Application, Infrastructure); verificar que compila y que el host lo registra
- [x] 1.4 Agregar `tests/Nexgate.ArchitectureTests` con las reglas de dependencia (módulos solo por Contracts, Domain sin Infrastructure, BuildingBlocks sin módulos); verificar con `dotnet test` y comprobar que una referencia prohibida de prueba hace fallar el test

## 2. Infraestructura local

- [x] 2.1 Crear `infra/docker-compose.yml` con PostgreSQL y Mosquitto y `.env.example`; verificar con `docker compose config` (sintaxis) y probando la misma configuración con un PostgreSQL 16 y un Mosquitto reales
- [x] 2.2 Documentar en `README.md` el arranque del entorno (requisitos, comandos, puertos); verificar siguiendo el README desde cero
- [x] 2.3 Ejecutar `docker compose up -d` en una máquina con acceso a Docker Hub; verificar que PostgreSQL y Mosquitto quedan `healthy`, que `dotnet run --project src/Nexgate.Host` responde 200 en `/health/ready` y que `dotnet test` pasa contra esa base

## 3. Convenciones de la API (`platform-api`)

- [x] 3.1 Implementar `/health/live` y `/health/ready` (con chequeo de PostgreSQL); verificar con tests de integración contra PostgreSQL real, incluyendo el caso con la base caída
- [x] 3.2 Implementar el manejador global de errores en formato problem details con código estable y correlación; verificar con tests de integración de 500, 400 de validación y ausencia de detalles internos
- [x] 3.3 Implementar el prefijo de versión `/api/v1` y un endpoint de ejemplo bajo ese prefijo; verificar con tests que la ruta sin prefijo devuelve 404
- [x] 3.4 Implementar el middleware de correlación (`X-Correlation-Id`) con validación del valor entrante; verificar con tests de los tres escenarios (válido, ausente, malformado)
- [x] 3.5 Configurar OpenTelemetry y logs estructurados con campo de organización opcional y sin datos personales; verificar con tests de los scopes de log (correlación y organización), con un test de que no se registran datos personales y revisando el log JSON de una ejecución local

## 4. Cierre

- [x] 4.1 Ejecutar todos los tests y el build limpio desde cero; verificar `dotnet build` y `dotnet test` verdes
- [x] 4.2 Actualizar `docs/05-arquitectura-tecnica.md` si la estructura final difiere de lo documentado; verificar revisando el diff

## Workflow follow-up

- Archivar el cambio con `openspec archive add-solution-foundation`, para que `platform-api` pase a `openspec/specs/`. (Hecho el 2026-11-07.)
