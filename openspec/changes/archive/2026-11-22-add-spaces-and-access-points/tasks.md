# Tasks

## 1. Módulo y persistencia

- [x] 1.1 Crear `Nexgate.Domain.Spaces`, `Nexgate.Service.Spaces.Contracts`, `Nexgate.Service.Spaces` y `Nexgate.Data.Spaces`, agregarlos a `Nexgate.slnx` y a las reglas de `Nexgate.Test.Architecture` (capas y dependencia `Service.Spaces → Service.Tenancy.Contracts`); verificar con `dotnet build` y las pruebas de arquitectura
- [x] 1.2 Entidades `Space` y `AccessPoint` con sus reglas (raíz de tipo sitio, tipos, nombre, profundidad máxima, destino distinto del origen, modo de fallo obligatorio) y nombres públicos en el Contracts; verificar con pruebas unitarias del dominio
- [x] 1.3 `SpacesDbContext` y migración del schema `spaces` con `ltree`, las tablas, claves compuestas, índices únicos y `CHECK` de la decisión 3 del design, `EnableTenantIsolation` y `GrantAppAccess`; registrar el contexto en el migrador; verificar con `migrate` sobre una base limpia (confirmar que el rol owner puede crear la extensión o mover la creación al script de `infra/`), `has-pending-model-changes` sin cambios y la prueba de catálogo de RLS

## 2. Espacios

- [x] 2.1 Puerto `ISiteReferences` en `Data.Spaces` implementado en `Service.Spaces` con `ISiteDirectory`; stores de espacios con traducción de violaciones de índices únicos a conflictos; verificar con pruebas de integración del store
- [x] 2.2 Servicio de espacios (alta, listado en orden de árbol, consulta, modificación, eliminación con `space_has_dependents`) y su registro en `ModuleExtensions`; verificar con pruebas unitarias del orden de árbol
- [x] 2.3 Permisos `spaces.read` y `spaces.manage` en `Permissions` y en el catálogo de Identity (administrador ambos, guardia lectura); verificar con la prueba de `GET /me/permissions` por rol y el escenario `Permissions by role` actualizado
- [x] 2.4 `SpacesController` con las operaciones de espacios de la decisión 6 y modelos en `Models/Spaces`; verificar con pruebas de integración de todos los escenarios de `space-tree` (incluidos 404 idénticos, alcance por sitio con un guardia, 403 de gestión y dos raíces en paralelo)

## 3. Puntos de acceso y camino

- [x] 3.1 Stores y servicio de puntos de acceso (alta, listado, consulta, modificación, eliminación) con `access_point_name_conflict`; verificar con pruebas de integración del store, incluida la clave foránea que impide un destino de otro sitio por SQL directo
- [x] 3.2 Consulta del camino por ancestros con `ltree` y `ISpaceDirectory` en el Contracts (`GetSpaceAsync`, `GetAccessPointAsync`, `GetAccessPathAsync`); verificar con pruebas de integración del ejemplo del 5B (camino completo, camino a un intermedio, otra rama excluida, camino vacío)
- [x] 3.3 `AccessPointsController` y `GET /spaces/{id}/access-path`; verificar con pruebas de integración de todos los escenarios de `access-points` y de `Space deletion` con puntos relacionados

## 4. Aislamiento, contrato y documentación

- [x] 4.1 Pruebas de aislamiento como `nexgate_app` con dos organizaciones sobre `spaces.space` y `spaces.access_point` (lectura sin filtro y escritura con otra organización); verificar con `dotnet test`
- [x] 4.2 Regenerar `api/nexgate-v1.openapi.json` con `NEXGATE_UPDATE_OPENAPI=1`, revisar el diff (operaciones, `enum` de tipos y modo de fallo, respuestas de error) y verificar la prueba de respuestas declaradas
- [x] 4.3 Actualizar `docs/02-dominio-y-modelo-de-datos.md` (tablas de Spaces con nombres reales, marcar el módulo como implementado), `docs/05-arquitectura-tecnica.md` si el diseño final difiere, y `docs/08-plan-de-cambios-openspec.md` (cambio 4 implementado, nuevo cambio 4b `add-space-scoped-roles` con la nota de alcance por espacio); verificar releyendo contra los specs
- [x] 4.4 Build limpio y todas las pruebas; verificar `dotnet build` sin warnings y `dotnet test` en verde

## Notas de implementación

- El camino a un destino se obtiene de las etiquetas del `path` del destino (los identificadores de sus ancestros) en lugar del operador `@>` de `ltree` (design, decisión 2). El índice GiST sobre `path` queda para consultas de descendientes.
- El rol owner crea la extensión `ltree` desde la migración (es una extensión de confianza): no hizo falta cambiar `infra/`.
- El transformador de esquemas del contrato copia también `AllowedValues` como `enum`; además de los tipos de Spaces, `CreateSiteRequest.type` gana su `enum` (cambio compatible del contrato).
- Se ajustaron dos pruebas existentes que fijaban los permisos del guardia (`RoleScopeTests`, `MembershipTests`), porque ahora incluye `spaces.read`.
- Verificación final: build sin warnings, 320 pruebas en verde (46 de arquitectura y 274 de la API contra PostgreSQL real), `migrate` idempotente y `has-pending-model-changes` sin cambios.

## Workflow follow-up

- Al archivar: los specs `space-tree` y `access-points` se crean y `role-scopes` se modifica; `docs/02`, `docs/05` y `docs/08` ya quedaron actualizados.
- Siguiente cambio sugerido: `add-space-scoped-roles` (4b) o `add-access-evaluator` (5), que usa `ISpaceDirectory.GetAccessPathAsync`.
