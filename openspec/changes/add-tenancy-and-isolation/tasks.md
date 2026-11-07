# Tasks

## 1. Roles de base de datos y migraciones

- [x] 1.1 Agregar `infra/postgres/init/01-roles.sh` (roles `nexgate_owner` y `nexgate_app` con `NOBYPASSRLS`; la función `public.nexgate_current_org()` la crea el migrador, ver design decisión 1), montarlo en `docker-compose.yml` y sumar las variables a `.env.example`; verificar recreando el volumen y comprobando con `psql` los atributos de los roles (`\du`)
- [x] 1.2 Agregar EF Core con Npgsql, la cadena `ConnectionStrings:Migrations` y el comando `migrate` del host que aplica las migraciones de todos los módulos con el rol owner; verificar ejecutando el comando dos veces (la segunda no hace cambios) y con un test de integración
- [x] 1.3 Agregar el helper de migraciones `EnableTenantIsolation` y los `GRANT` al rol app; verificar con un test unitario del SQL generado
- [x] 1.4 Actualizar el fixture de pruebas para crear una base limpia, migrarla con el rol owner y conectarse con el rol app (`NEXGATE_TEST_PG`, `NEXGATE_TEST_PG_OWNER`); verificar con `dotnet test`

## 2. Contexto de organización y aislamiento

- [x] 2.1 Implementar el middleware de tenancy (claim `org_id` y mecanismo de desarrollo `X-Nexgate-Organization` detrás de `Tenancy:DevelopmentAccess:Enabled`), ubicado antes del de telemetría; verificar con tests de los tres escenarios de `Active organization resolution` y que la telemetría lleva `organization.id`
- [x] 2.2 Bloquear el arranque si el mecanismo de desarrollo está habilitado fuera de `Development`; verificar con un test de arranque
- [x] 2.3 Implementar el interceptor que fija `app.org_id` en la sesión al abrir cada conexión (vacía si no hay organización; ver design decisión 2); verificar con un test de integración con pool de tamaño 1 (la organización no se filtra a la transacción siguiente)
- [x] 2.4 Implementar el `DbContext` base con `ITenantOwned`, filtro global y asignación y protección de `OrganizationId`; verificar con tests unitarios y de integración
- [x] 2.5 Agregar la respuesta 403 `organization_context_required` para operaciones sin organización activa; verificar con test de integración

## 3. Módulo Tenancy

- [x] 3.1 Crear el módulo `Tenancy` (Contracts, Domain, Application, Infrastructure) con las entidades `Organization` y `Site`, sus validaciones (nombre, tipo, zona horaria IANA) y la migración del schema `tenancy` con las políticas RLS de la decisión 4 del design; verificar con tests unitarios del dominio y aplicando la migración
- [x] 3.2 Implementar `POST /api/v1/platform/organizations` (con `app.platform_operation`) y `GET /api/v1/organization`; verificar con tests de integración de los escenarios de `Organization creation` y `Active organization details`
- [x] 3.3 Implementar `GET/POST /api/v1/sites` y `GET/PUT /api/v1/sites/{id}`; verificar con tests de integración de los escenarios de `Site creation`, `Site listing and details` y `Site modification`, incluyendo los de otra organización

## 4. Pruebas de aislamiento

- [x] 4.1 Tests de integración como `nexgate_app` con dos organizaciones: SQL crudo sin filtro devuelve solo filas propias, escritura con `organization_id` ajeno falla, sin organización activa devuelve cero filas; verificar con `dotnet test`
- [x] 4.2 Test de que el rol app no puede `ALTER TABLE`, desactivar RLS ni `SET ROLE nexgate_owner`; verificar con `dotnet test`
- [x] 4.3 Guardia de catálogo: test que falla si una tabla de un schema de negocio no tiene RLS habilitado, forzado y con política; verificar con `dotnet test` y comprobar que una tabla de prueba sin RLS lo hace fallar
- [x] 4.4 Test de 404 idéntico (estado y cuerpo, salvo la correlación) para recurso ajeno e inexistente; verificar con `dotnet test`

## 5. Retiro de `Sample` y cierre

- [x] 5.1 Mover las pruebas de convenciones de `platform-api` a endpoints de Tenancy y a un endpoint de error registrado solo desde los tests; eliminar el módulo `Sample`; verificar con `dotnet test` y con las pruebas de arquitectura
- [x] 5.2 Actualizar `README.md` (roles, cadenas de conexión, comando `migrate`, recrear el volumen) y `docs/05-arquitectura-tecnica.md` si el diseño final difiere; verificar siguiendo el README desde cero
- [x] 5.3 Ejecutar build limpio y todos los tests; verificar `dotnet build` y `dotnet test` verdes

## Notas de implementación

- La forma de fijar la organización activa cambió respecto del diseño original (design, decisión 2): por sesión al abrir cada conexión, en vez de local a la transacción.
- Se agregó el escenario de nombre de sitio duplicado (409 `site_name_conflict`). El escenario de "otra organización" en `Active organization details` se reformuló, porque no hay endpoint para consultar una organización por id.
- `dotnet-ef` quedó como herramienta local (`dotnet-tools.json`).
- Verificación final: build limpio sin warnings, 91 pruebas en verde (10 de arquitectura y 81 del host contra PostgreSQL 16 real), y prueba manual de la API levantada con el rol `nexgate_app`.

## Workflow follow-up

- Al archivar: actualizar `docs/02-dominio-y-modelo-de-datos.md` con los nombres en inglés de las tablas de Tenancy y `organization_id`, y registrar en `add-identity-and-roles` la tarea de quitar o restringir el mecanismo de desarrollo.
