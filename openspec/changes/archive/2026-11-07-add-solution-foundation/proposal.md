# Proposal

## Why

Todavía no existe código. Antes de implementar tenancy, acceso o dispositivos hace falta una base común: la solución .NET con la estructura de monolito modular, la infraestructura local reproducible y las convenciones transversales de la API. Hacerlo primero evita que cada módulo invente sus propias reglas de errores, versionado, observabilidad y dependencias entre módulos.

## What Changes

- Crear la solución .NET con el host de la API, el proceso de workers y los proyectos compartidos (`BuildingBlocks`), siguiendo la estructura de `docs/05-arquitectura-tecnica.md` (D-27).
- Dejar definido el esqueleto de un módulo (Contracts, Domain, Application, Infrastructure) y una prueba de arquitectura que impide referencias directas entre módulos que no sean a sus `*.Contracts`.
- Agregar infraestructura local con `docker-compose`: PostgreSQL y un broker MQTT (Mosquitto) para desarrollo (D-28, D-30).
- Establecer las convenciones transversales de la API: endpoints de salud, versionado, formato de errores y correlación de solicitudes.
- Agregar telemetría básica (logs estructurados y trazas) preparada para incluir la organización activa en cada traza.
- Documentar cómo levantar el entorno de desarrollo.

Fuera de alcance: tenancy, identidad, RLS, espacios, acceso, dispositivos, frontend y firmware. Cada uno es un cambio propio (ver `docs/08-plan-de-cambios-openspec.md`).

## Capabilities

### New Capabilities

- `platform-api`: convenciones observables de la API (salud, versionado, errores y correlación) que todos los módulos heredan.

### Modified Capabilities

(ninguna)

## Impact

- Código nuevo: solución .NET, proyectos `Nexgate.Host`, `Nexgate.Workers`, `BuildingBlocks`, y pruebas de arquitectura.
- Infraestructura: `infra/docker-compose.yml` con PostgreSQL y Mosquitto.
- Dependencias: SDK de .NET (LTS vigente), Docker para el entorno local.
- Seguridad y multitenancy: no hay datos de negocio todavía. El diseño deja previsto el contexto de organización en trazas y logs para no tener que cambiarlo después.
- Decisiones relacionadas: D-27, D-28, D-29 (solo lo que afecta al contrato de la API), D-30.
