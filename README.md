# Nexgate

Plataforma **multitenant** de **control de acceso físico**. Primer alcance: edificios de Córdoba (Argentina). Diseñada con un modelo genérico para extenderse a countries, barrios cerrados, empresas y casas sin tocar el núcleo.

> "Nexgate" es el nombre de trabajo (coincide con la carpeta del proyecto).

**Estado:** Fase 1, prototipo técnico. Implementados: base de la solución (`add-solution-foundation`) y tenancy con aislamiento por organización (`add-tenancy-and-isolation`).

## Idea en una frase

Alta y baja de credenciales desde un panel web, con puertas inteligentes (ESP32) que se actualizan solas por WiFi, funcionan sin internet y reportan todo lo que pasa. Un solo modelo (personas, unidades, espacios y otorgamientos) sirve para un edificio, un country o una empresa.

## Documentación

| Documento | Contenido |
|---|---|
| [01 Decisiones](docs/01-decisiones.md) | Registro de decisiones tomadas y preguntas abiertas |
| [02 Dominio y modelo de datos](docs/02-dominio-y-modelo-de-datos.md) | Entidades, relaciones y esquema por módulo |
| [03 Acceso, reglas y flujos](docs/03-acceso-reglas-y-flujos.md) | Motor de otorgamientos, invitaciones, presencia, morosidad |
| [04 Dispositivos](docs/04-dispositivos.md) | Hardware, firmware, protocolo, offline, OTA, seguridad |
| [05 Arquitectura técnica](docs/05-arquitectura-tecnica.md) | Backend .NET, PostgreSQL, multitenancy, frontend, infraestructura |
| [06 Privacidad y legal](docs/06-privacidad-y-legal.md) | Ley 25.326, padrón único, retención, derechos de los titulares |
| [07 Roadmap y MVP](docs/07-roadmap-mvp.md) | Fases, criterios de salida y próximos pasos |
| [08 Plan de cambios y flujo OpenSpec](docs/08-plan-de-cambios-openspec.md) | Cómo desarrollamos con OpenSpec y la lista ordenada de cambios planificados |

## Principios

1. **Un solo modelo, muchos contextos.** Edificio, country y empresa son configuración, no código distinto.
2. **Un solo motor de permisos.** Todo permiso es un *otorgamiento*; gana siempre la denegación.
3. **La puerta decide sola.** Los dispositivos funcionan offline con una política compilada y firmada.
4. **Simple por diseño.** Copiamos el vocabulario de los sistemas grandes (Lenel, Genetec, Salto, Brivo) y dejamos afuera lo que complica sin aportar valor.
5. **Privacidad desde el diseño.** Padrón único cifrado, sin enumeración, retención limitada.
6. **Monolito modular, una base de datos.** Preparado para separar microservicios después.

## Desarrollo local

Requisitos: SDK de .NET 10 (ver `global.json`) y Docker.

```bash
# 1. Levantar PostgreSQL y Mosquitto
docker compose --env-file infra/.env.example -f infra/docker-compose.yml up -d

# 2. Aplicar las migraciones (con el rol owner)
dotnet run --project src/Nexgate.Host -- migrate

# 3. Correr la API (http://localhost:5095)
dotnet run --project src/Nexgate.Host

# 4. Verificar
curl http://localhost:5095/health/live      # el proceso responde
curl http://localhost:5095/health/ready     # la base de datos acepta conexiones

# 5. Pruebas (usan PostgreSQL real)
dotnet test
```

**Roles de base de datos.** `infra/postgres/init/01-roles.sh` crea dos roles al inicializar el volumen:
`nexgate_owner` (migraciones, dueño de las tablas) y `nexgate_app` (la aplicación: sin propiedad y sin poder saltear RLS).
La cadena `ConnectionStrings:Default` usa `nexgate_app` y `ConnectionStrings:Migrations` usa `nexgate_owner`.
El script solo corre al crear el volumen: si ya tenías el volumen de una versión anterior, recrealo con
`docker compose --env-file infra/.env.example -f infra/docker-compose.yml down -v` y volvé a levantarlo.

**Organización activa en desarrollo.** Hasta que exista la autenticación, la organización activa se indica con el
encabezado `X-Nexgate-Organization`, y eso solo funciona si se habilita a mano (la aplicación no arranca si se lo
habilita fuera de `Development`):

```bash
Tenancy__DevelopmentAccess__Enabled=true dotnet run --project src/Nexgate.Host
curl -X POST http://localhost:5095/api/v1/platform/organizations -H "Content-Type: application/json" -d '{"name":"Consorcio Demo"}'
curl http://localhost:5095/api/v1/sites -H "X-Nexgate-Organization: <id de la organización>"
```

**Pruebas.** Cada corrida crea una base nueva (`nexgate_test_...`), la migra con el rol owner, se conecta con el rol de la
aplicación y la borra al terminar. Las cadenas salen de `src/Nexgate.Host/appsettings.json`, o de las variables
`NEXGATE_TEST_PG_OWNER` (owner) y `NEXGATE_TEST_PG` (aplicación).

**Migraciones nuevas.** `dotnet tool restore` instala `dotnet-ef`. Ejemplo:
`dotnet ef migrations add <Nombre> --project src/Modules/<Modulo>/<Modulo>.Infrastructure --startup-project src/Nexgate.Host --context <Modulo>DbContext --output-dir Migrations`.
Toda tabla de negocio debe llamar a `EnableTenantIsolation` y el schema a `GrantAppAccess`; una prueba lo verifica.

Estructura: `src/Nexgate.Host` (API), `src/Nexgate.Workers` (procesos en segundo plano), `src/BuildingBlocks` (piezas compartidas),
`src/Modules/<Modulo>` (un proyecto por capa: Contracts, Domain, Application, Infrastructure), `tests/`, `infra/`.
Módulos: `Tenancy` (organizaciones y sitios).

## Desarrollo con OpenSpec

Todo el desarrollo pasa por [OpenSpec](https://github.com/Fission-AI/OpenSpec): primero se acuerda el cambio (propuesta, specs, diseño y tareas) y después se implementa.

- `openspec/specs/`: comportamiento acordado (fuente de verdad). `openspec/changes/`: cambios en curso.
- `openspec/config.yaml`: contexto y reglas del proyecto para los artefactos.
- Ver el plan completo de cambios en [08](docs/08-plan-de-cambios-openspec.md).

La CLI es opcional para consultar el estado (`npm install -g @fission-ai/openspec@latest`, luego `openspec list` y `openspec view`).
