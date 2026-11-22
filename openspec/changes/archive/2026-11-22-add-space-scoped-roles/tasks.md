# Tasks

## 1. Alcance en el permiso efectivo

- [x] 1.1 `PermissionGrant` con `SpaceId` y `PermissionScope` (`CoversSite`, `ReachesSite`, `Covers`) en `ICurrentPermissions.ScopeFor`, conservando `SitesFor`; verificar con pruebas unitarias de cobertura, alcance de sitio, subárbol y suma de alcances
- [x] 1.2 `ISpaceDirectory.GetSpacesAsync(ids)` y `ancestorIds` en `SpaceDto`; verificar con una prueba de integración del directorio y con el listado de espacios
- [x] 1.3 `ISpaceUsage` en `Service.Spaces.Contracts` y su consulta en la eliminación de espacios (409 `space_has_dependents`); verificar con una prueba con una implementación de prueba registrada

## 2. Identity

- [x] 2.1 `ScopeType.Space`, `RoleScope.Space`, nombres públicos y `spaceId` en solicitudes, DTOs e invitaciones; `IOrganizationReferences.SpaceExistsAsync` con `ISpaceDirectory`; verificar con pruebas de integración de asignar e invitar con alcance de espacio (válido, ajeno, inexistente, sin identificador) y de aceptar
- [x] 2.2 Resolución de permisos con alcance de espacio (una consulta para los sitios, espacio inexistente sin permisos); verificar con pruebas de integración de `GET /me/permissions` con espacios
- [x] 2.3 Implementación de `ISpaceUsage` (asignaciones de cualquier vigencia e invitaciones pendientes) y migración con los índices `ix_role_assignment_scope` e `ix_invitation_scope`; verificar con pruebas de eliminación bloqueada y permitida después de revocar, `migrate` y `has-pending-model-changes`

## 3. Controllers

- [x] 3.1 Sitios con `ReachesSite` para leer y `CoversSite` para gestionar; verificar con los escenarios de `Site operations authorized by role scope`
- [x] 3.2 Espacios y camino con la regla del subárbol (listar, leer, crear con padre o raíz, modificar, eliminar); verificar con los escenarios de `Space permissions and site scope`
- [x] 3.3 Puntos de acceso con la regla del destino (listar, leer, crear, modificar, eliminar; origen libre en el sitio); verificar con los escenarios de `Access point permissions and site scope`
- [x] 3.4 Escenarios transversales de `role-scopes`: suma de alcances y alcance de espacio sin operaciones de organización (miembros, invitaciones, alta de sitios); verificar con pruebas de integración

## 4. Contrato y documentación

- [x] 4.1 Regenerar `api/nexgate-v1.openapi.json` y revisar que el diff solo agregue (`space` en `scopeType`, `spaceId`, `ancestorIds`, `spaces` en permisos); verificar con las pruebas de contrato
- [x] 4.2 Actualizar `docs/02-dominio-y-modelo-de-datos.md` (alcance de espacio en `role_assignment` e `invitation`), `docs/05-arquitectura-tecnica.md` (autorización por alcance y el puerto de uso de espacios) y `docs/08-plan-de-cambios-openspec.md`; verificar releyendo contra los specs
- [x] 4.3 Build limpio y todas las pruebas; verificar `dotnet build` sin warnings y `dotnet test` en verde

## Notas de implementación

- `SpaceDto` expone el camino con `ancestorIds` y un método `PathIds()` (no se serializa) que usan los controllers para la regla del subárbol.
- La lógica de alcance de Spaces quedó en `SpacesScope` (`Controllers/Spaces/SpacesResults.cs`), compartida por los dos controllers.
- El listado de sitios con alcance de espacio pasa al servicio los sitios que alcanza el permiso (enteros o por alguno de sus espacios).
- La tarea 1.3 se verificó con la implementación real de Identity (asignación e invitación pendiente) en lugar de una implementación de prueba: cubre lo mismo de punta a punta.
- La fábrica de pruebas acepta `spaceId` al invitar; `InvitationToken` suma `SpaceId`.
- Verificación final: build sin warnings, todas las pruebas en verde (12 nuevas en `SpaceScopeTests`), `migrate` aplicado y `has-pending-model-changes` sin cambios.
