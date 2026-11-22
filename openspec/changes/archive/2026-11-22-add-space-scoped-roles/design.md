# Design

## Context

Identity guarda las asignaciones de rol con `scope_type` (organization, site) y `scope_id`, y resuelve en cada solicitud los permisos efectivos como pares (permiso, sitio), donde sitio nulo es alcance de organización (`PermissionGrant` en `Common.BuildingBlocks/Security`). Los controllers filtran con `ICurrentPermissions.SitesFor(permiso)`. Spaces (cambio 4) dejó el árbol con `path` inmutable y el contrato `ISpaceDirectory`; su design (decisión 7) previó un puerto para que otros módulos bloqueen la eliminación de un espacio que usan. Ver `proposal.md` para la motivación y el alcance.

## Goals / Non-Goals

**Goals**
- Un rol con alcance de espacio que aplique a su subárbol, con una sola regla de cobertura para todos los recursos.
- Que los alcances existentes (organización y sitio) se comporten exactamente igual.
- Que ningún espacio usado como alcance quede eliminado por debajo de una asignación.

**Non-Goals**
- Permisos de miembros con alcance menor que la organización.
- Cambios en el catálogo de roles y permisos.

## Decisions

### 1. El permiso efectivo lleva su alcance completo

`PermissionGrant(Permission, SiteId, SpaceId)`:

| Alcance | `SiteId` | `SpaceId` |
|---|---|---|
| Organización | nulo | nulo |
| Sitio | el sitio | nulo |
| Espacio | el sitio que contiene el espacio | el espacio |

`ICurrentPermissions` suma `ScopeFor(permiso)`, que devuelve un `PermissionScope`:

- `CoversSite(siteId)`: organización o el sitio entero. Es lo que hoy devuelve `SitesFor`, que se conserva con el mismo significado para no cambiar a quienes lo usan.
- `ReachesSite(siteId)`: además, cualquier espacio de ese sitio. Sirve para la lectura del sitio y para decidir entre 403 y 404.
- `Covers(siteId, pathIds)`: cubre el sitio entero o alguno de los identificadores del camino del recurso (sus ancestros y él mismo) es un espacio del alcance. Es la regla del subárbol.

`Has(permiso)` sigue siendo "con cualquier alcance", así que `RequirePermission` deja pasar a quien tiene el permiso en algún espacio y la acción decide dónde aplica. `HasAtOrganization` no cambia: miembros, invitaciones y alta de sitios siguen exigiendo alcance de organización.

- **Alternativa descartada:** guardar en el permiso el `path` del espacio para comparar prefijos. Ata `Common` al formato interno de Spaces; con los identificadores del camino del recurso alcanza.

### 2. El sitio del espacio se resuelve en cada solicitud

`IdentityAccessResolver` carga las asignaciones vigentes y, si alguna tiene alcance de espacio, pide en una sola consulta los espacios a `ISpaceDirectory.GetSpacesAsync(ids)` (método nuevo del contrato) para obtener su sitio. Un espacio que ya no existe no otorga nada (falla cerrado).

- Solo cuesta una consulta extra a quien tiene alcances de espacio; los demás no cambian.
- **Alternativa descartada:** guardar el sitio en la asignación (columna desnormalizada). Los espacios no cambian de sitio, pero sería una segunda fuente de verdad y una migración de datos sin necesidad.

### 3. Validación del alcance con Spaces

- Dominio: `ScopeType.Space` y `RoleScope.Space(spaceId)`; en la base, `scope_type = 'space'` y `scope_id` = el espacio. La columna ya es texto: no hay cambio de esquema salvo índices (decisión 6).
- `IOrganizationReferences` (puerto de `Data.Identity`) suma `SpaceExistsAsync(spaceId)`, que implementa `Service.Identity` con `ISpaceDirectory` bajo RLS de la organización activa. Se usa al asignar, al invitar y al aceptar (si el espacio desapareció entre la invitación y la aceptación, la aceptación responde como invitación inválida, igual que hoy con un sitio).
- Solicitudes: `AssignRoleRequest` e `InviteRequest` suman `spaceId`; `scopeType` admite `space`. Respuestas: `RoleAssignmentDto` e `IssuedInvitation` suman `spaceId` (el `siteId` queda solo para el alcance de sitio).
- Nueva dependencia `Service.Identity → Service.Spaces.Contracts`, permitida por las reglas de capas.

### 4. Filtrado en los controllers

- **Sitios:** `listSites` y `getSite` usan `ReachesSite` con `sites.read`; `updateSite` sigue con `CoversSite` de `sites.manage`, así que un alcance de espacio responde 403 sobre un sitio que ve. `createSite` no cambia (`AtOrganization`).
- **Espacios:** `SpaceDto` suma `ancestorIds` (identificadores desde la raíz hasta el padre). Con eso:
  - Listar: si `CoversSite`, todo el sitio; si no, solo los espacios cuyo camino toca un espacio del alcance. Sitio que no alcanza: 404.
  - Leer, modificar, eliminar y camino: `Covers(siteId, ancestorIds + id)`; fuera de la lectura, 404; dentro de la lectura pero fuera de la gestión, 403.
  - Crear: con padre, el padre tiene que estar cubierto por la gestión, si no responde 400 `ParentId` igual que un padre inexistente (no revela ramas que no ve). Sin padre (raíz): exige `CoversSite` de gestión; 403 si alcanza el sitio, 404 si no.
- **Puntos de acceso:** se cubre un punto si se cubre su espacio de destino.
  - Listar: los del sitio cuyo destino está entre los espacios visibles (se obtienen con el listado de espacios del sitio).
  - Crear y modificar: el destino tiene que estar cubierto por la gestión, si no 400 `ToSpaceId`. El origen puede ser cualquier espacio del sitio: una puerta de hall se entra desde la raíz.
  - El camino a un espacio cubierto se devuelve completo, también con los puntos de los ancestros.
- **Alternativa descartada:** pasar el alcance al servicio de Spaces. El Contracts no puede depender de `Common`, así que habría que duplicar el tipo de alcance en el contrato; con `ancestorIds` el controller decide con los datos del recurso, igual que hoy con `siteId`.

### 5. Uso de espacios por otros módulos

`Service.Spaces.Contracts` define `ISpaceUsage.IsUsedAsync(spaceId)`. Identity lo implementa: un espacio está en uso si es alcance de alguna asignación de rol (vigente o no) o de una invitación ni aceptada ni vencida. El servicio de Spaces recibe todas las implementaciones registradas (`IEnumerable<ISpaceUsage>`) y responde 409 `space_has_dependents` si alguna lo usa.

- Las asignaciones vencidas también bloquean: siguen visibles en la lista de miembros y apuntarían a un espacio inexistente. Para eliminar el espacio, primero se revoca la asignación.
- Carrera conocida: una asignación creada en paralelo con la eliminación del espacio puede quedar apuntando a un espacio que ya no existe. No da permisos (decisión 2) y se ve en la lista de miembros; se acepta para no sumar bloqueos entre schemas.
- **Alternativa descartada:** que Spaces consulte las tablas de Identity. Rompe el aislamiento de módulos (ningún join ni lectura entre schemas).

### 6. Base de datos

Migración de Identity con índices para la consulta de uso: `ix_role_assignment_scope (scope_type, scope_id)` e `ix_invitation_scope (scope_type, scope_id)`. Las tablas no cambian y mantienen su política RLS por `organization_id`.

### 7. Consulta de permisos

`PermissionResponse` suma `spaces: [{ spaceId, siteId }]`. `organization` y `siteIds` conservan su significado (alcance de organización y sitios enteros). Un permiso con alcance de organización no lista sitios ni espacios, como hoy no lista sitios.

## Cómo se prueba

- **Unitarias:** `PermissionScope` (cubrir sitio, alcanzar sitio, subárbol, suma de alcances) y el catálogo con alcance de espacio.
- **Integración con PostgreSQL real**, con un sitio que tiene raíz, torre A con piso y unidad, y torre B:
  - Asignar e invitar con alcance de espacio, incluidos espacio ajeno o inexistente (400 idéntico) y falta del identificador.
  - Guardia con alcance en la torre A: ve el sitio, la torre A, su piso y sus puntos; no ve la raíz ni la torre B (404 idéntico a inexistente); el camino a la unidad incluye el portón.
  - Administrador con alcance en la torre A: crea, renombra y elimina dentro; 400 con padre o destino fuera; 403 al crear raíz o modificar el sitio; 403 en miembros e invitaciones.
  - Suma de alcances (torre A más sitio B).
  - Eliminación bloqueada por asignación e invitación pendiente, y permitida después de revocar.
  - `GET /me/permissions` con espacios.
  - Los escenarios existentes de alcance de organización y de sitio siguen pasando sin cambios.
- **Arquitectura:** la dependencia nueva `Service.Identity → Service.Spaces.Contracts` pasa las reglas; Spaces no referencia a Identity.
- **Contrato:** regenerar el documento OpenAPI y revisar que los cambios sean solo agregados.

## Risks / Trade-offs

- **Lógica de alcance en los controllers:** crece con cada recurso nuevo que cuelgue del árbol. Si aparecen más (unidades, otorgamientos), conviene extraer un filtro compartido por recurso; por ahora son dos controllers.
- **Consulta extra por solicitud** para quien tiene alcances de espacio. Es una consulta por identificador; si molesta, se cachea por solicitud (ya se resuelve una sola vez por solicitud).
