# Design

## Context

`add-solution-foundation` dejó la solución, las convenciones de la API y un punto de extensión para la organización activa (`ICurrentOrganization`, leído desde el contexto HTTP y ya volcado a logs y trazas). No hay acceso a datos de negocio ni RLS. `docs/05-arquitectura-tecnica.md` fija el enfoque: RLS en PostgreSQL como defensa principal, `set_config('app.org_id', ..., true)` por transacción, rol de aplicación que no es dueño de las tablas y filtros globales de EF Core como segunda barrera. Ver `proposal.md` para la motivación.

La identidad llega en el cambio siguiente (`add-identity-and-roles`). Este cambio tiene que dejar el aislamiento funcionando y probado sin depender de ella.

## Goals / Non-Goals

**Goals**
- Que cualquier módulo nuevo obtenga aislamiento por organización con una convención y una prueba que lo exija, sin código repetido.
- Que un error de la aplicación (consulta sin filtro, `organization_id` equivocado) no pueda filtrar datos entre organizaciones.
- Que el mecanismo funcione con pool de conexiones y falle cerrado.

**Non-Goals**
- Autenticación y autorización por rol o alcance.
- Aislamiento por sitio o por espacio: se resuelve en la capa de aplicación con las asignaciones de rol (cambio 3).
- Contexto de organización en los workers.

## Decisions

### 1. Dos roles de base de datos

| Rol | Uso | Atributos |
|---|---|---|
| `nexgate_owner` | Migraciones; dueño de schemas y tablas | `LOGIN`, sin superusuario |
| `nexgate_app` | Conexión de la aplicación en ejecución | `LOGIN NOSUPERUSER NOBYPASSRLS`, solo `SELECT/INSERT/UPDATE/DELETE` sobre tablas y `USAGE` sobre schemas y secuencias |

- `infra/postgres/init/01-roles.sh` crea ambos roles al inicializar el contenedor y le pasa la propiedad de la base a `nexgate_owner`. Las contraseñas de desarrollo salen de `.env.example`. `nexgate_owner` tiene `CREATEDB` para que las pruebas creen su propia base.
- La función `public.nexgate_current_org()` no la crea el script de roles sino el migrador (idempotente, antes de las migraciones de los módulos), así existe en cualquier base que se migre, incluidas las de prueba.
- Cadenas de conexión: `ConnectionStrings:Default` (rol app) y `ConnectionStrings:Migrations` (rol owner). Las pruebas pueden sobrescribir ambas (`NEXGATE_TEST_PG`, `NEXGATE_TEST_PG_OWNER`).
- Cada migración otorga los permisos al rol app sobre lo que crea (`GRANT` explícito, más `ALTER DEFAULT PRIVILEGES` en cada schema).
- **Alternativa descartada:** un solo rol con `FORCE ROW LEVEL SECURITY`. `FORCE` aplica RLS al dueño, pero el dueño puede ejecutar `ALTER TABLE ... DISABLE ROW LEVEL SECURITY`; un rol sin propiedad no puede.

### 2. Organización activa en la sesión, fijada al abrir cada conexión

*Ajustado durante la implementación.* La propuesta original era fijar la variable local a la transacción con una unidad de trabajo por solicitud. Se cambió porque EF ejecuta las lecturas sin transacción: habría obligado a abrir una transacción en cada solicitud solo para leer.

- Un interceptor de conexión de EF Core ejecuta `SELECT set_config('app.org_id', @org, false)` cada vez que EF abre una conexión, con el valor de `ICurrentOrganization`. EF abre y cierra la conexión en cada operación (salvo dentro de una transacción), así que cada uso recibe el valor de la solicitud en curso.
- Sin organización activa el interceptor fija la cadena vacía de forma explícita, en lugar de no hacer nada: una conexión reutilizada nunca conserva la organización de una solicitud anterior.
- Además, Npgsql reinicia el estado de la sesión (`DISCARD ALL`) al devolver la conexión al pool. Una prueba con pool de tamaño 1 verifica que la organización no pasa a la siguiente solicitud, ni por EF ni con SQL directo.
- Las marcas de un solo uso, como `app.platform_operation`, sí son locales a la transacción (`is_local = true`).
- **Alternativa descartada:** variable local a la transacción con una unidad de trabajo por solicitud. Es la opción del diseño original; obliga a una transacción por solicitud, incluso en lecturas.
- **Alternativa descartada:** una conexión o base por organización. Multiplica conexiones y migraciones; D-28 eligió una base con RLS.

### 3. Política RLS estándar

Toda tabla de negocio tiene la columna `organization_id uuid NOT NULL` y:

```sql
ALTER TABLE <schema>.<tabla> ENABLE ROW LEVEL SECURITY;
ALTER TABLE <schema>.<tabla> FORCE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation ON <schema>.<tabla>
  USING (organization_id = nexgate_current_org())
  WITH CHECK (organization_id = nexgate_current_org());
```

- `public.nexgate_current_org()` es una función `STABLE` que devuelve `NULLIF(current_setting('app.org_id', true), '')::uuid`. El `NULLIF` es necesario: después de una transacción que fijó el valor, la variable queda como cadena vacía en esa conexión y `''::uuid` lanzaría error. Con `NULL`, la comparación nunca es verdadera y la consulta devuelve cero filas (falla cerrado).
- `WITH CHECK` impide insertar o mover filas a otra organización.
- Un helper de migraciones (`migrationBuilder.EnableTenantIsolation("schema", "tabla")`) genera estas sentencias para que todas las tablas usen la misma política.
- Los nombres de columnas y tablas van en inglés (Q-04): `organization_id`, no `organizacion_id`. `docs/02` se actualiza al archivar.

### 4. Tabla de organizaciones

`tenancy.organization` es la raíz y no tiene `organization_id`: su `id` cumple ese papel.

| Tabla | Columnas | RLS |
|---|---|---|
| `tenancy.organization` | `id uuid PK`, `name text`, `status text` (`active`), `created_at timestamptz` | Lectura y modificación: `id = nexgate_current_org()`. Inserción: solo con `app.platform_operation = 'on'` |
| `tenancy.site` | `id uuid PK`, `organization_id uuid FK`, `name text`, `type text` (`building`, `gated_community`, `company`, `house`), `time_zone text`, `configuration jsonb`, `created_at`, `updated_at` | Política estándar (decisión 3) |

- Crear una organización es una operación de plataforma: no hay organización activa todavía. El servicio de plataforma fija `app.platform_operation` local a la transacción y la política de inserción de `organization` lo exige. Ninguna otra tabla lo reconoce.
- Se reconoce el límite: RLS protege contra errores de la aplicación, no contra una aplicación comprometida (que puede fijar cualquier variable). Esa amenaza se mitiga con el rol sin privilegios, la auditoría y, más adelante, autorización por rol.
- **Alternativa descartada:** crear organizaciones con el rol owner desde la API. Obligaría a la aplicación en ejecución a tener credenciales con poder de saltear RLS.
- `name` de sitio único por organización (índice único `(organization_id, lower(name))`).

### 5. Filtros globales como segunda barrera

- Las entidades de negocio implementan `ITenantOwned { Guid OrganizationId }`. Un `DbContext` base de `BuildingBlocks` aplica a todas un filtro global `OrganizationId == currentOrganization` y, al guardar, asigna `OrganizationId` desde el contexto y rechaza cambios a ese valor.
- Si RLS falla por mala configuración, el filtro todavía evita la fuga; si el filtro se omite (`IgnoreQueryFilters`, SQL crudo), RLS lo cubre.
- **Alternativa descartada:** solo filtros de EF. Una consulta con SQL crudo o un módulo nuevo que olvide el filtro filtraría datos.

### 6. Resolución de la organización activa

- Un middleware de tenancy, ubicado antes del de telemetría, completa `HttpContext.Items[OrganizationId]` (el punto de extensión ya existente).
- Fuente confiable: el claim `org_id` del usuario autenticado. Este cambio deja el lector del claim listo; nadie lo emite hasta `add-identity-and-roles`.
- **Mecanismo de desarrollo:** con `Tenancy:DevelopmentAccess:Enabled = true`, el middleware acepta el encabezado `X-Nexgate-Organization` (un GUID) y habilita los endpoints de plataforma. Está apagado por defecto, y si se habilita en un entorno que no es `Development` la aplicación no arranca. Se elimina o se restringe cuando exista identidad.
- Sin organización activa, los endpoints de organización responden 403 con código `organization_context_required`.
- **Alternativa descartada:** esperar a identidad para exponer endpoints. Dejaría el aislamiento sin probar de punta a punta y mezclaría dos cambios grandes.
- **Alternativa descartada:** resolver la organización por subdominio. Un usuario puede pertenecer a varias organizaciones (D-04); el selector se resuelve con el token.

### 7. Endpoints

| Método y ruta | Descripción |
|---|---|
| `POST /api/v1/platform/organizations` | Crear organización (plataforma) |
| `GET /api/v1/organization` | Organización activa |
| `GET /api/v1/sites` | Sitios de la organización activa |
| `POST /api/v1/sites` | Crear sitio |
| `GET /api/v1/sites/{id}` | Detalle |
| `PUT /api/v1/sites/{id}` | Modificar nombre, zona horaria y configuración |

- Recursos de otra organización responden 404 con el mismo cuerpo que un identificador inexistente.
- Zona horaria validada contra la base tzdb de NodaTime, que viaja con la aplicación. Se descartó `TimeZoneInfo` porque la solución usa `InvariantGlobalization` y, en Windows, la conversión de identificadores IANA depende de ICU y del sistema operativo.
- Nombre de sitio duplicado en la organización (sin distinguir mayúsculas): 409 con código `site_name_conflict`.
- La configuración del sitio (`jsonb`) se acepta como objeto JSON libre en este cambio; su esquema lo definen los cambios que la usen (por ejemplo, plantillas de acceso por rol).

### 8. Migraciones

- EF Core migrations por módulo, cada una con su schema y su tabla de historial (`tenancy.__ef_migrations_history`).
- Se aplican con el rol owner mediante un comando explícito (`dotnet run --project src/Nexgate.Host -- migrate`) y en las pruebas desde el fixture. La aplicación en ejecución no migra al arrancar, porque su rol no tiene permisos para hacerlo.
- **Alternativa descartada:** migrar al arrancar. Exigiría que el proceso de la API use credenciales de owner.

### 9. Retiro del módulo `Sample`

Las pruebas de convenciones (errores, validación, versionado, telemetría) pasan a usar los endpoints de Tenancy. El endpoint que fuerza un error 500 se mueve a un endpoint de prueba registrado solo desde el proyecto de tests.

## Cómo se prueba

- **Unitarias:** validación de entrada de organizaciones y sitios; asignación y protección de `OrganizationId` en el `DbContext` base.
- **Integración con PostgreSQL real, conectando como `nexgate_app`:**
  - Dos organizaciones con sitios: cada una ve solo los suyos por la API y por SQL crudo sin filtro.
  - Insertar o actualizar con `organization_id` ajeno falla por la política.
  - Sin organización activa: cero filas, sin error.
  - Misma conexión física (pool de tamaño 1): la organización de una transacción no se ve en la siguiente.
  - El rol app no puede `ALTER TABLE`, `DISABLE ROW LEVEL SECURITY` ni `SET ROLE nexgate_owner`.
  - 404 idéntico para recurso ajeno e inexistente.
- **Guardia de catálogo:** una prueba consulta `pg_class`/`pg_policy` y falla si alguna tabla de los schemas de negocio (todos salvo `people` y las tablas de historial de migraciones) no tiene `relrowsecurity`, `relforcerowsecurity` y al menos una política. Al agregar un módulo, la prueba lo cubre sin cambios.
- **Arranque:** la aplicación no arranca con el mecanismo de desarrollo habilitado fuera de `Development`.

## Risks / Trade-offs

- [Acceso a datos que no pase por EF (SQL directo con otra conexión) y que no fije la variable] → Falla cerrado (cero filas). Para acceder a datos de negocio hay que usar el `DbContext` del módulo.
- [Costo de `set_config` cada vez que se abre una conexión] → Es una consulta local sin acceso a disco; se revisa si aparece en los perfiles.
- [El mecanismo de desarrollo llega a producción] → Apagado por defecto, bloqueo de arranque fuera de `Development` y tarea explícita en `add-identity-and-roles` para quitarlo.
- [Diferencia de nombres entre `docs/02` (español) y el esquema (inglés)] → Se actualiza `docs/02` al archivar.

## Migration Plan

No hay datos previos. Se agrega el script de roles a `infra/`; quien ya tenga el volumen de PostgreSQL de desarrollo debe recrearlo (`docker compose down -v`) o ejecutar el script a mano, y el README lo indica. Rollback: revertir la rama y recrear el volumen.

## Open Questions

- ¿Quién opera la plataforma (alta de organizaciones) antes de que exista el rol `PlatformAdmin`? Propuesta: solo el mecanismo de desarrollo en este cambio; en el cambio 3, `PlatformAdmin`.
