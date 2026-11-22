# Proposal

## Why

D-03 define el alcance de un rol como organización, sitio **o espacio**. Hoy solo existen los dos primeros, así que no se puede expresar un caso habitual: el guardia de la torre A que no debe ver la torre B, o el encargado de un barrio dentro de un country que administra sus espacios pero no los del resto. `add-spaces-and-access-points` dejó el árbol de espacios y separó este trabajo en un cambio propio (su design, decisión 8) porque toca Identity entero: asignaciones, invitaciones, la forma de los permisos efectivos y su filtrado.

## What Changes

- **Alcance de espacio** en asignaciones de rol e invitaciones: `scopeType = space` con `spaceId`. El espacio tiene que existir en la organización activa; uno ajeno o inexistente responde igual (400).
- **Cobertura**: un permiso con alcance de espacio aplica a ese espacio y a todos sus descendientes (su subárbol), nunca a sus ancestros ni a otras ramas.
- **Efecto en cada recurso**:
  - Sitios: quien tiene lectura de sitios con alcance de espacio ve el sitio que contiene ese espacio (para poder navegarlo). Ningún alcance de espacio permite crear ni modificar sitios.
  - Espacios: lectura y gestión limitadas al subárbol. Crear la raíz de un sitio exige alcance de sitio u organización.
  - Puntos de acceso: se ven y se gestionan los que dan acceso a un espacio del subárbol. El camino a un espacio del subárbol se devuelve completo, incluidos los puntos de sus ancestros (son las puertas que hay que cruzar para llegar).
  - Miembros e invitaciones siguen exigiendo alcance de organización. La protección del último administrador sigue contando solo administradores con alcance de organización.
- **Consulta de permisos** (`GET /me/permissions`): cada permiso suma la lista de espacios donde aplica, con el sitio de cada uno.
- **Eliminación de espacios**: un espacio referido por una asignación de rol o por una invitación pendiente no se puede eliminar (409 `space_has_dependents`). Es el primer uso del puerto previsto en el design de Spaces (decisión 7), por el que otros módulos declaran que usan un espacio.

Fuera de alcance:
- Alcance de espacio para permisos de miembros (invitar o dar de baja dentro de un subárbol): siguen siendo de organización.
- Roles nuevos o cambios en el catálogo de permisos por rol.
- Acceso físico: los roles son permisos administrativos sobre la API. El acceso a las puertas es exclusivo de los Otorgamientos (`add-access-evaluator`, D-13).
- Pantallas del panel (`add-admin-panel-shell`).

## Capabilities

### New Capabilities

(ninguna)

### Modified Capabilities

- `role-scopes`: el alcance de espacio en las asignaciones, la regla de cobertura del subárbol y los espacios en la consulta de permisos.
- `organization-membership`: invitaciones con alcance de espacio.
- `organizations-and-sites`: un alcance de espacio permite leer el sitio que lo contiene, nunca gestionarlo.
- `space-tree`: permisos limitados al subárbol y eliminación bloqueada por roles e invitaciones.
- `access-points`: permisos limitados a los puntos que dan acceso al subárbol.

## Impact

- Código modificado:
  - `Common.BuildingBlocks/Security`: el permiso efectivo lleva su alcance completo (organización, sitio o espacio) y `ICurrentPermissions` expone el alcance por espacio.
  - Identity: dominio (`ScopeType.Space`), validación del alcance con `Service.Spaces.Contracts`, resolución de permisos, invitaciones y la implementación del puerto de uso de espacios.
  - Spaces: el servicio filtra por el alcance del llamador y consulta el puerto de uso antes de eliminar.
  - Controllers de sitios, espacios, puntos de acceso y `me`.
- Dependencias entre módulos: `Service.Identity → Service.Spaces.Contracts` (nueva, permitida por las reglas de capas). Spaces no conoce a Identity: define el puerto en su Contracts y recibe sus implementaciones.
- Base de datos: sin tablas nuevas. `scope_type` admite el valor `space` (la columna ya es texto) y se agrega un índice por `scope_id` para la consulta de uso.
- Contrato OpenAPI: `scopeType` suma `space`, las solicitudes suman `spaceId` y las respuestas de asignaciones, invitaciones y permisos suman el espacio. Son cambios compatibles.
- **Multitenancy:** el espacio se valida bajo RLS de la organización activa, igual que el sitio. Un recurso fuera del subárbol del llamador responde 404, igual que uno inexistente.
- **Seguridad:** un alcance más chico nunca amplía permisos: se suma a los de organización y sitio del mismo usuario y solo restringe dónde aplican los suyos. No hay cambios en el token: los permisos se siguen evaluando en el servidor en cada solicitud.
- **Privacidad:** sin datos personales nuevos.
- Decisiones relacionadas: D-03 (completa), D-05, D-06.
