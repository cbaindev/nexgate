# Design

## Context

No hay código. La arquitectura objetivo está en `docs/05-arquitectura-tecnica.md`: monolito modular en .NET, un schema de PostgreSQL por módulo, una sola base y comunicación entre módulos por contratos y eventos. Este cambio crea el esqueleto y las convenciones transversales; no define ningún módulo de negocio. Ver `proposal.md` para la motivación.

## Goals / Non-Goals

**Goals**
- Que cualquier módulo nuevo se cree copiando una estructura conocida y quede sujeto a las mismas reglas.
- Que el entorno local levante con un solo comando.
- Que las reglas de dependencia entre módulos se verifiquen automáticamente desde el primer día.

**Non-Goals**
- Implementar RLS, identidad o cualquier tabla de negocio (cambios posteriores).
- Elegir el proveedor de despliegue en la nube.

## Decisions

### 1. Estructura de la solución

```
nexgate/
  src/
    Nexgate.Host/          API, SignalR, composición
    Nexgate.Workers/       MQTT bridge, compilador, outbox, retención
    BuildingBlocks/        outbox, tenancy, mensajería, errores, telemetría
    Modules/<Modulo>/      <Modulo>.Contracts, .Domain, .Application, .Infrastructure
  tests/
    Nexgate.ArchitectureTests/
  infra/docker-compose.yml
```

- Se crea un módulo de ejemplo mínimo (`Sample`) solo para demostrar la estructura y probar las reglas de arquitectura. Se elimina cuando llegue el primer módulo real.
- **Alternativa descartada:** una solución por módulo. Aumenta la fricción de desarrollo y no aporta valor hasta que un módulo se extraiga como servicio (D-27 lo prevé como evolución).

### 2. Reglas de dependencia verificadas por pruebas

Pruebas automáticas leen los `.csproj` de `src/` y verifican que:
- Un módulo solo referencia a otro por su proyecto `*.Contracts`.
- `Domain` no depende de `Application` ni de `Infrastructure`, y no usa frameworks ni paquetes externos. `Contracts` tampoco.
- `BuildingBlocks` no depende de ningún módulo ni de `Host`/`Workers`.

Se eligió leer los proyectos y no inspeccionar ensamblados (NetArchTest): así la regla falla aunque la referencia todavía no se use en el código, y no agrega una dependencia. Las reglas están separadas del acceso a archivos para probarlas también con grafos sintéticos que contienen violaciones.

**Alternativas descartadas:** convención en code review (no escala y falla en silencio) y NetArchTest (solo ve referencias efectivamente usadas).

### 3. Errores como problem details (RFC 9457)

Un único manejador global convierte excepciones y validaciones en `application/problem+json`, con un código de error estable (`code`) y el identificador de correlación. Las excepciones no controladas nunca devuelven mensajes internos.

**Alternativa descartada:** formato de error propio. Los clientes y herramientas ya entienden problem details.

### 4. Versionado por prefijo de ruta

Los endpoints de negocio viven bajo `/api/v1/...`. Los de salud (`/health/live`, `/health/ready`) quedan fuera del versionado. Es la opción más simple de probar y de cachear.

**Alternativa descartada:** versionado por encabezado. Más difícil de usar desde navegador y de depurar.

### 5. Salud

`live` no verifica dependencias. `ready` verifica la conexión a PostgreSQL. El broker MQTT se agrega a `ready` del proceso de workers cuando exista el puente de dispositivos, no en este cambio.

### 6. Correlación y telemetría

- Encabezado `X-Correlation-Id`: se acepta si es un valor corto de caracteres seguros; si no, se genera uno nuevo.
- Se usa el ID del activity de .NET como respaldo, de modo que el mismo identificador aparezca en logs y trazas.
- OpenTelemetry para trazas y métricas, y logs estructurados. El atributo `organization.id` se agrega cuando exista contexto de organización (cambio de tenancy). Se prohíbe registrar documentos, nombres, teléfonos y credenciales; se prueba con una regla de revisión y un test del formateador de logs.

### 7. Infraestructura local

`docker-compose` con PostgreSQL y Mosquitto, con variables en `.env.example`. Mosquitto arranca sin TLS en local (con una nota explícita); TLS y autenticación por dispositivo se definen en el cambio del protocolo de dispositivos.

**Alternativa descartada:** instalar servicios en la máquina. Poco reproducible.

### 8. Versión del SDK

Se fija con `global.json` en .NET 10 (LTS; SDK 10.0.100 con avance a la última versión de la misma banda de funcionalidades). `Directory.Build.props` activa nullable y warnings como errores. Los ensamblados de módulos se nombran `Nexgate.Modules.<Modulo>.<Capa>` desde un `Directory.Build.props` propio de `src/Modules`.

### 9. Validación de entrada

Un filtro de endpoint valida con DataAnnotations y responde 400 con un problem details que lista cada campo inválido. Se escribe por la vía de `IProblemDetailsService` para que lleve siempre el código y la correlación.

**Alternativa descartada:** la validación integrada de minimal APIs; no descubre tipos definidos en proyectos de módulos distintos del host sin atributos adicionales.

### 10. Pruebas de integración

Usan PostgreSQL real. La cadena de conexión sale de `ConnectionStrings:Default` (la que crea `docker-compose`) o de `NEXGATE_TEST_PG`. La cadena de la base se resuelve al usar el servicio y no al arrancar, para que cada prueba pueda apuntar a otra base (por ejemplo, una caída).

**Alternativa descartada por ahora:** Testcontainers. Exige un Docker funcional en cada máquina; se puede sumar después para CI.

## Risks / Trade-offs

- [Esqueleto sobredimensionado para un solo desarrollador] → Solo se crean los proyectos necesarios; el módulo `Sample` es descartable.
- [Las reglas de arquitectura frenan el desarrollo al principio] → Mensajes de error claros indicando el contrato que falta; las reglas se pueden relajar con una decisión explícita.
- [Divergencia entre entorno local y producción] → Mismas versiones mayores de PostgreSQL y broker en local y en la nube; documentadas en el README de desarrollo.

## Migration Plan

No aplica: no hay sistema previo. Rollback: eliminar la rama.

## Open Questions

- Proveedor del repositorio y del pipeline de CI (GitHub o Azure DevOps). Las tareas dejan un script portable de build y test, y el pipeline se agrega cuando se decida, sin cambiar este diseño.
- Nombre definitivo del producto (hoy "Nexgate", de trabajo): afecta nombres de proyectos, y es barato de cambiar ahora.
