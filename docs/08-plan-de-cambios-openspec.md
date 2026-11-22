# 08 Plan de cambios y flujo de trabajo con OpenSpec

Todo el desarrollo se hace con **OpenSpec** (desarrollo guiado por especificaciones): primero se acuerda *qué* construir en un cambio con propuesta, specs, diseño y tareas, y recién después se implementa.

## Cómo se usa

El flujo de OpenSpec lo maneja Claude desde la conversación: pedís "proponé el cambio X" o "implementá el cambio Y" y se ejecuta la CLI `openspec`, se escriben los artefactos y el código, y se sincroniza todo con esta carpeta. No hace falta instalar nada ni usar Claude Code.

| Paso | Qué pasa |
|---|---|
| 1. Explorar (opcional) | Se piensa la idea antes de comprometerse |
| 2. Proponer | Se generan proposal, specs, design y tasks. Se revisan y se ajustan |
| 3. Implementar | Se implementan las tareas y se van tildando, con build y tests en verde |
| 4. Archivar | Los specs del cambio se fusionan en `openspec/specs/` |

Opcional, para consultar el estado desde tu terminal (Node.js 20.19 o superior): `npm install -g @fission-ai/openspec@latest`, y luego `openspec list`, `openspec list --specs`, `openspec status --change <nombre>`, `openspec validate --strict` y `openspec view`.

## Reglas del proyecto

- **Un cambio por vez en implementación**, con alcance chico (idealmente de una a tres sesiones de trabajo).
- Cada propuesta cita los **IDs de decisión** de [01 Decisiones](01-decisiones.md).
- Si una decisión cambia, se actualiza `docs/01-decisiones.md` y se ajusta el cambio afectado.
- Los specs describen **comportamiento observable**; lo técnico va en `design.md`.
- Los documentos 02 a 07 son el diseño de referencia. Al archivar un cambio, si el comportamiento difiere de lo documentado, se corrige el documento.
- El contexto y las reglas por artefacto viven en `openspec/config.yaml`.

## Plan de cambios

El estado de cada cambio se ve con `openspec list`. Los nombres son propuestos y se pueden ajustar al proponerlos.

### Fase 1: prototipo técnico

| # | Cambio | Capacidades (specs) | Decisiones |
|---|---|---|---|
| 1 | `add-solution-foundation` (**implementado y archivado**) | `platform-api` | D-27, D-28, D-30 |
| 1b | `adopt-smt-layout-and-controllers` (**implementado y archivado**): estructura en capas numeradas al estilo SMT y API con controllers | `platform-api` | D-27, D-32, D-33 |
| 2 | `add-tenancy-and-isolation` (**implementado y archivado**, en paralelo a 1b sobre la estructura anterior) | `tenancy/organizations-and-sites`, `tenancy/tenant-isolation` | D-02, D-28 |
| 3 | `add-identity-and-roles` (**implementado y archivado**, ídem) | `identity/authentication`, `identity/role-scopes` | D-03, D-04, D-05 |
| 3b | `add-openapi-contract` (agregado; **implementado y archivado**, ídem: contrato OpenAPI y Swagger UI) | `platform-api` | D-29, D-34 |
| 3c | `integrate-modules-into-layers` (agregado; **implementado y archivado**): lleva Tenancy, Identity y el contrato OpenAPI a la estructura de 1b, con controllers y sin cambios de contrato | `platform-api` | D-32, D-33, D-34 |
| 4 | `add-spaces-and-access-points` (**implementado y archivado**): módulo Spaces con el árbol de espacios, los puntos de acceso, el camino a un destino y los permisos `spaces.read` y `spaces.manage` | `space-tree`, `access-points`, `role-scopes` | D-06, D-09 |
| 4b | `add-space-scoped-roles` (agregado; **implementado y archivado**): rol con alcance de espacio que cubre su subárbol (D-03), separado del cambio 4 porque modifica Identity entero | `role-scopes`, `organization-membership`, `organizations-and-sites`, `space-tree`, `access-points` | D-03 |
| 5 | `add-access-evaluator` (**implementado y archivado**): sujetos mínimos (Occupancy, adelantados), horarios, credenciales con HMAC por sitio, otorgamientos, política compilada por punto de acceso, decisión y simulación, con vectores de prueba compartidos con el firmware | `subjects`, `access-grants`, `policy-compilation`, `access-evaluation`, `role-scopes`, `space-tree`, `access-points` | D-13, D-15 |
| 6 | `add-device-sync-protocol` | `devices/device-provisioning`, `devices/policy-sync`, `events/event-ingestion` | D-30 |
| 7 | `add-admin-panel-shell` (base de la SPA progresiva: instalación, navegación por rol y cliente generado del contrato) | `web/admin-shell` | D-05, D-29 |

### Fase 2: MVP edificios

| # | Cambio | Capacidades (specs) | Decisiones |
|---|---|---|---|
| 8 | `add-people-registry` | `people/person-registry`, `people/subject-linking` | D-21, D-22, D-25 |
| 9 | `add-occupancy-model` (el registro mínimo de sujetos ya existe, de `add-access-evaluator`: este cambio lo completa) | `occupancy/units-and-occupancies`, `occupancy/memberships`, `occupancy/role-access-templates` | D-06, D-07, D-10, D-11 |
| 10 | `add-resident-onboarding` | `occupancy/resident-registration` | D-07 |
| 11 | `add-guest-invitations` | `access/invitations`, `occupancy/contact-book`, `access/path-derivation` | D-08, D-09, D-14, D-15, D-23 |
| 12 | `add-signed-qr-credentials` | `access/qr-credentials` | D-14 |
| 13 | `add-guard-temporary-access` | `access/temporary-access` | D-16 |
| 14 | `add-owner-temporary-access-requests` | `access/owner-access-requests` | D-12 |
| 15 | `add-presence-and-incidents` | `events/presence`, `events/incidents` | D-17, D-18 |
| 16 | `add-privacy-and-retention` | `privacy/retention`, `privacy/subject-rights`, `privacy/guest-data-visibility` | D-24, D-26 |
| 17 | `add-device-ota-and-health` | `devices/ota`, `devices/health` | D-30 |
| 18 | `add-rf-retrofit` | `devices/rf-import` | D-31 |
| 19 | `add-resident-pwa` (vistas de residente dentro de la misma SPA progresiva) | `web/resident-app` | D-29 |

### Fase 3: madurez y expansión

| # | Cambio | Notas | Decisiones |
|---|---|---|---|
| 20 | `add-delinquency-restrictions` | Restricción de espacios comunes por morosidad (nunca la puerta de la unidad ni el acceso principal) | D-19 |
| 21 | `add-strict-presence-and-capacity` | Antipassback duro y aforo | D-17 |
| 22 | `add-ble-credentials` | Llave en el celular | |
| 23 | `add-billing` | Se define con el modelo de cobro | D-20 |

## Orden recomendado

El cambio 5 (`add-access-evaluator`) es el corazón del producto y el más fácil de probar de forma aislada. Conviene implementarlo apenas existan tenancy y espacios, con los vectores de prueba pensados desde el principio para que el firmware los reutilice.

## Preguntas abiertas

- ¿Proveedor del repositorio y del pipeline de CI (GitHub o Azure DevOps)?
- ¿Nombre definitivo del producto?
