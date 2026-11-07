# Proposal

## Why

Todo dato de negocio de Nexgate pertenece a una organización y ninguna organización puede ver datos de otra. Ese aislamiento tiene que existir antes que cualquier tabla de negocio (identidad, espacios, acceso), porque agregarlo después obliga a revisar cada consulta y cada migración. Además, la organización y el sitio son las raíces del modelo de dominio: todo lo demás cuelga de ellos.

## What Changes

- Crear el primer módulo real, **Tenancy**, con organizaciones y sitios (D-02). Un sitio es un edificio, country, empresa o casa, con zona horaria propia y configuración.
- Establecer el aislamiento por organización como mecanismo transversal (D-28):
  - Row-Level Security habilitado y forzado en toda tabla de negocio, con una política que filtra por la organización activa.
  - La aplicación se conecta con un rol de base de datos que no es dueño de las tablas ni puede saltear RLS; las migraciones corren con otro rol.
  - La organización activa se fija por transacción y, sin organización activa, las consultas no devuelven filas (falla cerrado).
  - Filtros globales en la capa de datos como segunda barrera.
  - Una prueba automática que falla si una tabla de negocio no tiene RLS habilitado, forzado y con política.
- Resolver la organización activa de cada solicitud y publicarla en el contexto ya previsto por `platform-api` (logs y trazas con `organization.id`).
- Endpoints REST bajo `/api/v1` para crear y consultar organizaciones (operación de plataforma) y para crear, listar, consultar y modificar sitios de la organización activa.
- Reemplazar el módulo de ejemplo `Sample` por Tenancy, como estaba previsto en `add-solution-foundation`.

Fuera de alcance:
- Autenticación, usuarios, membresía en varias organizaciones, selector de organización y roles por alcance (D-03, D-04): cambio `add-identity-and-roles`. Hasta entonces, la organización activa solo puede fijarse con un mecanismo de desarrollo deshabilitado por defecto (ver design).
- Suspensión o baja de organizaciones y sitios, y sus efectos sobre el acceso físico.
- Plan comercial, cupos y facturación (D-20, Q-02).
- Espacios, puntos de acceso y cualquier otra tabla de negocio: cada una llega con su cambio y hereda este mecanismo.
- Contexto de organización en los workers (procesos sin solicitud HTTP): se define cuando exista el primer trabajo en segundo plano con datos de negocio.

## Capabilities

### New Capabilities

- `organizations-and-sites`: alta y consulta de organizaciones y alta, consulta y modificación de sitios dentro de una organización.
- `tenant-isolation`: garantía observable de que ningún dato de negocio se lee ni se escribe fuera de la organización activa, incluso ante errores de la aplicación.

### Modified Capabilities

(ninguna; `platform-api` ya prevé la organización activa en la telemetría y este cambio la completa sin alterar sus requirements)

## Impact

- Código nuevo: módulo `Tenancy` (Contracts, Domain, Application, Infrastructure), infraestructura de tenancy y acceso a datos en `BuildingBlocks` (contexto de organización, fijación por transacción, filtros), migraciones del schema `tenancy`.
- Código eliminado: módulo `Sample`; sus pruebas de convenciones pasan a usar endpoints de Tenancy.
- Base de datos: schema `tenancy`, dos roles (`nexgate_owner` para migraciones, `nexgate_app` para la aplicación, sin `BYPASSRLS`), script de inicialización en `infra/`.
- Dependencias: EF Core con el proveedor de Npgsql.
- Configuración: nueva cadena de conexión para migraciones; la cadena `Default` pasa a usar el rol de la aplicación.
- **Multitenancy:** este cambio es el límite de aislamiento. RLS es la defensa principal y los filtros de la capa de datos la segunda. El acceso a recursos de otra organización responde como si no existieran (404), para no revelar su existencia.
- **Seguridad:** el rol de la aplicación no puede desactivar RLS ni modificar el esquema. El mecanismo de desarrollo para fijar la organización se rechaza fuera del entorno de desarrollo y queda apagado por defecto.
- **Privacidad:** organizaciones y sitios no contienen datos personales. No se registran nombres de organización en logs, solo identificadores.
- Decisiones relacionadas: D-02, D-28; prepara D-03 y D-04.
