# Nexgate

Plataforma **multitenant** de **control de acceso físico**. Primer alcance: edificios de Córdoba (Argentina). Diseñada con un modelo genérico para extenderse a countries, barrios cerrados, empresas y casas sin tocar el núcleo.

> "Nexgate" es el nombre de trabajo (coincide con la carpeta del proyecto). Cambiarlo es trivial mientras no haya código.

**Estado:** Fase 0, diseño. Todavía no hay código.

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

# 2. Correr la API (http://localhost:5095)
dotnet run --project src/02.Api/Nexgate.Api.Rest

# 3. Verificar
curl http://localhost:5095/health/live      # el proceso responde
curl http://localhost:5095/health/ready     # la base de datos acepta conexiones
curl http://localhost:5095/api/v1/sample/ping

# 4. Pruebas (usan PostgreSQL real; ver NEXGATE_TEST_PG abajo)
dotnet test
```

Las pruebas de integración usan la cadena de conexión `ConnectionStrings:Default` de `src/02.Api/Nexgate.Api.Rest/appsettings.json`
(la misma que crea `docker-compose`). Para apuntar a otra base, definir la variable de entorno `NEXGATE_TEST_PG`.

Estructura (capas numeradas, con un proyecto por módulo dentro de cada capa):

| Carpeta | Contenido |
|---|---|
| `src/01.Client` | Frontend (reservado) |
| `src/02.Api` | `Nexgate.Api.Rest` (API) y `Nexgate.Api.Workers` (procesos en segundo plano) |
| `src/03.Service` | `Nexgate.Service.<Modulo>` (servicios) y `Nexgate.Service.<Modulo>.Contracts` (contrato público del módulo) |
| `src/04.Data` | `Nexgate.Data.<Modulo>` (persistencia, cuando el módulo la tenga) |
| `src/05.Domain` | `Nexgate.Domain.<Modulo>` (dominio) |
| `src/06.Common` | `Nexgate.Common.BuildingBlocks` (piezas compartidas) |
| `src/07.Test` | Pruebas de la API y de arquitectura |

Además, `infra/` tiene la infraestructura local. `Sample` es un módulo de ejemplo descartable.

## Desarrollo con OpenSpec

Todo el desarrollo pasa por [OpenSpec](https://github.com/Fission-AI/OpenSpec): primero se acuerda el cambio (propuesta, specs, diseño y tareas) y después se implementa.

- `openspec/specs/`: comportamiento acordado (fuente de verdad). `openspec/changes/`: cambios en curso.
- `openspec/config.yaml`: contexto y reglas del proyecto para los artefactos.
- Ver el plan completo de cambios en [08](docs/08-plan-de-cambios-openspec.md).

La CLI es opcional para consultar el estado (`npm install -g @fission-ai/openspec@latest`, luego `openspec list` y `openspec view`).
