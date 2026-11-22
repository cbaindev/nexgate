# Design

## Context

Tenancy (organizaciones y sitios) e Identity (cuentas, roles por alcance de organización o sitio, permisos administrativos) están implementados sobre las capas SMT (D-32), con controllers REST (D-33) y contrato OpenAPI verificado (D-34). `docs/02-dominio-y-modelo-de-datos.md` define el árbol de espacios con `ltree`, el punto de acceso con un único atributo espacial importante (hacia qué espacio da acceso) y la derivación del camino por ancestros. Ver `proposal.md` para la motivación y el alcance.

## Goals / Non-Goals

**Goals**
- Un modelo físico por sitio que el evaluador de acceso (cambio 5) pueda consultar sin grafos: cobertura de un punto = el destino o sus ancestros.
- Que la base de datos garantice por sí sola que un espacio, su padre y los puntos que apuntan a él son del mismo sitio y de la misma organización.
- Reusar sin cambios el mecanismo de permisos por sitio que ya tienen los sitios.

**Non-Goals**
- Alcance de rol por espacio (decisión 8).
- Mover espacios entre padres.
- Atributos de presencia, aforo, cupos y morosidad del espacio; dispositivo y canal del punto.

## Decisions

### 1. Raíz explícita de tipo sitio

Cada sitio tiene a lo sumo un espacio raíz, de tipo `site` y sin padre; todos los demás espacios tienen padre. La raíz la crea el administrador como el primer espacio del sitio. Los puntos del perímetro (portón principal) dan acceso a la raíz.

- Así todo el árbol, incluido el perímetro, es uniforme: un otorgamiento futuro siempre apunta a un espacio o a un punto, y "acceso principal al sitio" (D-19) es "punto hacia la raíz".
- **Alternativa descartada:** crear la raíz automáticamente al crear el sitio. Exige un evento de integración de Tenancy a Spaces (outbox), que todavía no existe, o que Tenancy dependa de Spaces. Se puede sumar cuando exista el outbox sin cambiar el modelo.
- **Alternativa descartada:** que el perímetro sea "hacia el sitio" (`to_space_id` nulo). Obliga a un tercer tipo de destino en los otorgamientos y a casos especiales en la derivación.

### 2. Árbol con `parent_id` y `path` `ltree`

- `path` es la secuencia de identificadores desde la raíz, con una etiqueta por espacio: el UUID en hexadecimal sin guiones (las etiquetas de `ltree` admiten letras, dígitos y guion bajo). La profundidad es `nlevel(path) - 1`.
- Como no hay movimiento de espacios (fuera de alcance), `path` es inmutable: se calcula al crear, a partir del `path` del padre, dentro de la misma transacción.
- Ancestros o el mismo espacio: *ajustado durante la implementación*. Como las etiquetas del `path` son los identificadores de los ancestros, el camino se obtiene leyendo el `path` del destino y buscando los puntos cuyo `to_space_id` está en esa lista; no hace falta un operador de `ltree` ni recorrer el árbol. El índice GiST sobre `path` queda para las consultas de descendientes (`<@`) que van a necesitar el compilador y Occupancy.
- La extensión `ltree` la crea la migración del módulo con el rol owner (`CREATE EXTENSION IF NOT EXISTS ltree`). Se verificó que el rol owner puede crearla: `ltree` es una extensión de confianza (*trusted*) y el owner es dueño de la base, así que no hace falta tocar `infra/`.
- **Alternativa descartada:** solo `parent_id` con CTE recursiva. Funciona, pero cada consulta de cobertura recorre el árbol; el evaluador y el compilador la van a ejecutar muchas veces.
- **Alternativa descartada:** tabla de clausura. Más filas y más escrituras por espacio sin ventaja sobre `ltree` para un árbol que no se mueve.

### 3. Tablas

Schema `spaces`. Las dos tablas son datos de negocio: `organization_id uuid NOT NULL` y la política RLS estándar (`migrationBuilder.EnableTenantIsolation("spaces", ...)`, habilitada y forzada), más `GrantAppAccess("spaces")`. La prueba de catálogo las cubre sin cambios.

| Tabla | Columnas |
|---|---|
| `space` | id, organization_id, site_id, parent_id (nulo solo en la raíz), path (`ltree`), type (site, building, floor, unit, common_area, parking, lot, zone), name, is_common, created_at, updated_at |
| `access_point` | id, organization_id, site_id, name, type (door, gate, barrier, turnstile, elevator), to_space_id, from_space_id (nulo), fail_mode (fail_safe, fail_secure), created_at, updated_at |

Restricciones (la base garantiza las reglas de mismo sitio y de eliminación aunque la aplicación falle):

- `space`: clave única `(site_id, id)`; `(site_id, parent_id)` referencia a `space (site_id, id)` con `ON DELETE RESTRICT`, así el padre siempre es del mismo sitio.
- `access_point`: `(site_id, to_space_id)` y `(site_id, from_space_id)` referencian a `space (site_id, id)` con `ON DELETE RESTRICT`; `CHECK (from_space_id IS NULL OR from_space_id <> to_space_id)`.
- Únicos: una raíz por sitio (`UNIQUE (site_id) WHERE parent_id IS NULL`), nombre entre hermanos (`UNIQUE (parent_id, lower(name)) WHERE parent_id IS NOT NULL`), nombre de punto en el sitio (`UNIQUE (site_id, lower(name))`).
- `CHECK ((parent_id IS NULL) = (type = 'site'))` y `CHECK (nlevel(path) <= 11)`.
- El mismo sitio implica la misma organización: el sitio se valida contra la organización activa al crear y RLS impide escribir con otra.

No hay claves foráneas hacia `tenancy.site`: los módulos no se unen entre schemas (doc 05). El sitio se valida con un puerto (decisión 4).

### 4. Validación del sitio con un puerto

`Data.Spaces` define `ISiteReferences.SiteExistsAsync(siteId)`, que implementa `Service.Spaces` con `ISiteDirectory` de `Service.Tenancy.Contracts` (el mismo patrón que `IOrganizationReferences` en Identity). Pasa por RLS, así que un sitio de otra organización no existe. Un sitio inexistente o ajeno responde 404 en todas las operaciones anidadas bajo `/sites/{siteId}`.

### 5. Permisos y alcance por sitio

- `Permissions` de `Service.Identity.Contracts` suma `spaces.read` y `spaces.manage`. El catálogo de Identity los asigna: administrador ambos, guardia `spaces.read`, residente y empleado ninguno (verán su unidad por Occupancy).
- Se reutilizan `RequirePermission` y `ICurrentPermissions.SitesFor` sin cambios, con el mismo criterio que `SitesController`:
  - Sin el permiso en ningún sitio: 403 `permission_denied` (lo resuelve el atributo).
  - Sitio fuera del alcance de lectura: 404, igual que inexistente.
  - Sitio visible pero fuera del alcance de `spaces.manage`: 403.
- En las rutas por id (`/spaces/{id}`, `/access-points/{id}`) el controller primero obtiene el recurso (que ya pasa por RLS), toma su `site_id` y recién ahí aplica el alcance. Un recurso de un sitio fuera del alcance de lectura responde 404.
- **Alternativa descartada:** permisos separados para puntos de acceso (`access_points.manage`). Hoy los mismos roles administran ambos; se puede separar después sin romper el contrato agregando un permiso.

### 6. API

| Método y ruta | Operación | Permiso |
|---|---|---|
| `GET /api/v1/sites/{siteId}/spaces` | `listSpaces` | `spaces.read` |
| `POST /api/v1/sites/{siteId}/spaces` | `createSpace` | `spaces.manage` |
| `GET /api/v1/spaces/{id}` | `getSpace` | `spaces.read` |
| `PUT /api/v1/spaces/{id}` | `updateSpace` | `spaces.manage` |
| `DELETE /api/v1/spaces/{id}` | `deleteSpace` | `spaces.manage` |
| `GET /api/v1/spaces/{id}/access-path` | `getAccessPath` | `spaces.read` |
| `GET /api/v1/sites/{siteId}/access-points` | `listAccessPoints` | `spaces.read` |
| `POST /api/v1/sites/{siteId}/access-points` | `createAccessPoint` | `spaces.manage` |
| `GET /api/v1/access-points/{id}` | `getAccessPoint` | `spaces.read` |
| `PUT /api/v1/access-points/{id}` | `updateAccessPoint` | `spaces.manage` |
| `DELETE /api/v1/access-points/{id}` | `deleteAccessPoint` | `spaces.manage` |

- Espacio: `{ id, siteId, parentId, type, name, isCommon, depth }`. Alta: `{ name, type, parentId?, isCommon? }` (`isCommon` falso por omisión). Modificación: `{ name, type, isCommon }`.
- Punto: `{ id, siteId, name, type, toSpaceId, fromSpaceId, failMode }`. Alta y modificación con los mismos campos salvo `id` y `siteId`; `failMode` es obligatorio, sin valor por omisión.
- Camino: lista de puntos con la forma de punto más `toSpaceDepth`, ordenada por profundidad y nombre.
- Errores con código estable: `space_root_exists` (409), `space_name_conflict` (409), `space_has_dependents` (409), `access_point_name_conflict` (409). Las violaciones de los índices únicos por concurrencia se traducen al mismo 409, identificando la restricción por su nombre.
- Los modelos de solicitud viven en `Models/Spaces` con atributos de validación; los valores permitidos de `type` y `failMode` salen de `Service.Spaces.Contracts` y quedan como `enum` en el documento OpenAPI. Para eso, el transformador de esquemas que copia los atributos de los parámetros del constructor ahora copia también `AllowedValues`, así que el tipo de sitio (`CreateSiteRequest.type`) gana su `enum` en el contrato (cambio compatible).
- **Alternativa descartada:** rutas totalmente anidadas (`/sites/{siteId}/spaces/{id}`). Repiten un dato que el recurso ya tiene y obligan a validar que coincidan; las rutas por id son las que usarán los clientes después de listar.

### 7. Contrato para otros módulos

`Service.Spaces.Contracts` expone `ISpaceDirectory`, con RLS:

- `GetSpaceAsync(id)` y `GetAccessPointAsync(id)`: existencia, sitio y datos básicos.
- `GetAccessPathAsync(spaceId)`: los puntos del camino (decisión 2). Es la regla de cobertura de D-13 en un solo lugar.
- `GetCoveredAccessPointsAsync(spaceId)` queda para el cambio 5 si el compilador necesita la inversa (puntos alcanzados por un otorgamiento a un espacio); no se agrega antes de tener un consumidor.

**Eliminación con dependientes futuros:** cuando Occupancy, Access y Devices referencien espacios o puntos (por id, sin claves foráneas entre schemas), `Service.Spaces.Contracts` definirá un puerto `ISpaceUsage` que cada módulo implementa y registra; el servicio de Spaces los consulta antes de eliminar. Hoy no hay consumidores, así que no se crea.

### 8. Alcance de rol por espacio: cambio propio

El plan anotaba para este cambio agregar `scope_type = space` a `role-scopes`. Se propone separarlo en `add-space-scoped-roles` (4b), inmediatamente después, porque:

- Cambia la forma de los permisos efectivos (`PermissionGrant` hoy tiene solo `SiteId`) y su filtrado en todos los controllers, no solo en los de Spaces.
- Requiere decidir semánticas que hoy no tienen consumidor: si un guardia con alcance en una torre ve el sitio, qué espacios lista, qué pasa con las invitaciones y con la protección del último administrador.
- Identity pasaría a depender de `Service.Spaces.Contracts` para validar el alcance.

Mezclarlo con la creación del módulo hace este cambio mucho más grande y de revisión más difícil, sin habilitar nada que el cambio 5 necesite.

**Alternativa descartada:** incluirlo acá, como decía el plan.

### 9. Orden del listado

El listado carga los espacios del sitio y arma el orden de árbol en la capa de servicio (padre antes que hijos, hermanos por nombre con comparación de cultura invariante sin distinguir mayúsculas). Un edificio grande tiene cientos de espacios, no decenas de miles.

- **Alternativa descartada:** ordenar en SQL con una CTE recursiva que acumula nombres. Más difícil de leer y de probar para un volumen que no lo justifica.

## Cómo se prueba

- **Unitarias (Domain):** reglas de tipo y raíz, nombre, profundidad, mismo destino y origen; orden de árbol.
- **Integración con PostgreSQL real (`Nexgate.Test.Api`):** cada escenario de los specs por HTTP, incluidos 404 idénticos (cuerpo igual salvo la correlación) para recurso ajeno e inexistente, alcance por sitio con un guardia, y concurrencia de dos raíces y de nombres duplicados.
- **Aislamiento:** como `nexgate_app` con dos organizaciones, SQL directo sin filtro sobre `spaces.space` y `spaces.access_point` devuelve solo filas propias; insertar o actualizar con `organization_id` ajeno falla; las claves foráneas impiden un punto hacia un espacio de otro sitio aunque se escriba por SQL.
- **Catálogo:** la prueba existente de RLS cubre el schema nuevo; se agrega el schema a su lista si la lista es explícita.
- **Arquitectura:** el módulo entra en las reglas de capas y de dependencias (`Service.Spaces` solo ve `Service.Tenancy.Contracts`).
- **Contrato:** el documento OpenAPI se regenera con `NEXGATE_UPDATE_OPENAPI=1` y la prueba de respuestas declaradas cubre las operaciones nuevas.
- No hay dispositivos en este cambio; los escenarios sin conexión llegan con `add-device-sync-protocol`.

## Risks / Trade-offs

- **Sin movimiento de espacios:** corregir una estructura mal cargada exige borrar y recrear hojas. Aceptable mientras no haya ocupaciones ni otorgamientos; el movimiento se agrega con recálculo de `path` en una transacción.
- **Extensión `ltree`:** si un proveedor administrado la restringe, hay que habilitarla a mano; Neon, Supabase y Azure la ofrecen (verificar al elegir proveedor).
- **Raíz manual:** un sitio sin raíz no puede tener puntos de acceso. El panel (cambio 7) la puede crear al dar de alta el sitio.
