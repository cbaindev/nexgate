# Tasks

## 1. Sujetos (módulo Occupancy)

- [x] 1.1 Crear `Nexgate.Domain.Occupancy`, `Nexgate.Service.Occupancy(.Contracts)` y `Nexgate.Data.Occupancy`, agregarlos a la solución y a las reglas de arquitectura; verificar con `dotnet build` y las pruebas de arquitectura
- [x] 1.2 Entidad `Subject`, migración del schema `occupancy` con RLS, `ISubjectDirectory` y el puerto `ISubjectUsage`; verificar con `migrate`, `has-pending-model-changes` y la prueba de catálogo
- [x] 1.3 Permisos `access.read` y `access.manage` en `Permissions` y en el catálogo de Identity; verificar con la prueba de permisos por rol actualizada
- [x] 1.4 `SubjectsController` y modelos; verificar con pruebas de integración de los escenarios de `subjects` (incluidos permisos, alcance de sitio y de espacio, 404 idénticos)

## 2. Horarios, credenciales y otorgamientos (módulo Access)

- [x] 2.1 Crear los proyectos de Access y la configuración `Access:CredentialKey` (clave de desarrollo y validación de arranque fuera de `Development`); verificar con una prueba de arranque
- [x] 2.2 Horarios: dominio con validación de ventanas, tabla, servicio y `SchedulesController`; verificar con pruebas unitarias y los escenarios de `Schedules`
- [x] 2.3 Credenciales: normalización y HMAC por sitio, tabla con índice único, servicio y `CredentialsController`; verificar con pruebas unitarias del hash y los escenarios de `Credentials` (incluido que ninguna respuesta contiene el valor ni el hash)
- [x] 2.4 Otorgamientos: dominio, tabla, validación de sujeto, destino y horario por contrato, servicio y `GrantsController`; verificar con los escenarios de `Grants`
- [x] 2.5 Puertos de uso: Access implementa `ISpaceUsage`, `IAccessPointUsage` (nuevo, consultado al eliminar puntos) e `ISubjectUsage`; eliminación de horarios en uso; verificar con los escenarios de eliminación bloqueada de `space-tree`, `access-points`, `subjects` y `Schedules`

## 3. Compilación y decisión

- [x] 3.1 `ISpaceDirectory.GetSubtreeIdsAsync` e `ISiteDirectory.GetTimeZoneAsync`; verificar con pruebas de integración
- [x] 3.2 Compilador de la política v1 (cobertura, intersección de vigencias, tabla de desplazamientos, orden canónico y hash); verificar con pruebas unitarias y los escenarios de `policy-compilation`
- [x] 3.3 Algoritmo de decisión puro en `Domain.Access`; verificar con pruebas unitarias
- [x] 3.4 Vectores en `vectors/access-evaluation/v1` con su `README.md` y `AccessVectorsTests`; verificar que cada escenario de `Access decision` tiene al menos un vector y que todos pasan
- [x] 3.5 `GET /access-points/{id}/policy` y `POST /access-points/{id}/simulations`; verificar con los escenarios de `Access simulation` y de cobertura (`Grant coverage`) de punta a punta

## 4. Aislamiento, privacidad, contrato y documentación

- [x] 4.1 Pruebas de aislamiento con SQL directo sobre `occupancy.subject`, `access.schedule`, `access.credential` y `access.grant`; verificar con `dotnet test`
- [x] 4.2 Prueba de logs: ni el valor de una credencial, ni su hash, ni el alias de un sujeto aparecen en los logs de alta y simulación; verificar con `dotnet test`
- [x] 4.3 Regenerar `api/nexgate-v1.openapi.json` y revisar el diff; verificar con las pruebas de contrato
- [x] 4.4 Actualizar `docs/02` (tablas de Occupancy y Access implementadas), `docs/03` (orden de la denegación, HMAC por sitio, formato de la política), `docs/05` (módulos y dependencias) y `docs/08` (cambio 5 implementado; nota de sujetos adelantados en el cambio 9); verificar releyendo contra los specs
- [x] 4.5 Build limpio y todas las pruebas; verificar `dotnet build` sin warnings y `dotnet test` en verde

## Notas de implementación

- La política incluye todas las credenciales activas del sitio, no solo las que tienen entradas en ese punto (como dice el spec): una credencial válida sin permiso en esa puerta da `no_permission` y no `credential_invalid`. Se ajustó la decisión 6 del design.
- Los horarios de la política se ordenan por su forma canónica (no por primer uso), para que el orden no dependa del orden de los otorgamientos.
- El subárbol de un espacio se calcula con los identificadores del `path` de los espacios del sitio, en memoria, en vez de `<@` de `ltree`: alcanza para el tamaño de un sitio y evita traducir operadores de `ltree` sobre una propiedad con conversión.
- En `Development` la clave de credenciales tiene un valor fijo en código (`AccessOptions.DevelopmentCredentialKey`) en lugar de `appsettings.Development.json`; las pruebas usan una clave aleatoria por corrida.
- Los vectores se generaron con un script (25 casos) y se versionan como archivos: el script no forma parte del repositorio.
- Verificación final: build sin warnings, todas las pruebas en verde (incluidos los 25 vectores), `migrate` aplicado y `has-pending-model-changes` sin cambios en Occupancy y Access; el contrato OpenAPI solo agrega operaciones y esquemas.
