# Nexgate

Plataforma **multitenant** de **control de acceso físico**. Primer alcance: edificios de Córdoba (Argentina). Diseñada con un modelo genérico para extenderse a countries, barrios cerrados, empresas y casas sin tocar el núcleo.

> "Nexgate" es el nombre de trabajo (coincide con la carpeta del proyecto).

**Estado:** Fase 1, prototipo técnico. Implementados: base de la solución (`add-solution-foundation`), tenancy con aislamiento por organización (`add-tenancy-and-isolation`) e identidad con roles por alcance (`add-identity-and-roles`).

## Idea en una frase

Alta y baja de credenciales desde un panel web, con puertas inteligentes (ESP32) que se actualizan solas por WiFi, funcionan sin internet y reportan todo lo que pasa. Un solo modelo (personas, unidades, espacios y otorgamientos) sirve para un edificio, un country o una empresa.

## Documentación

| Documento | Contenido |
|---|---|
| [01 Decisiones](docs/01-decisiones.md) | Registro de decisiones tomadas y preguntas abiertas |
| [02 Dominio y modelo de datos](docs/02-dominio-y-modelo-de-datos.md) | Entidades, relaciones y esquema por módulo |
| [03 Acceso, reglas y flujos](docs/03-acceso-reglas-y-flujos.md) | Motor de otorgamientos, invitaciones, presencia, morosidad |
| [04 Dispositivos](docs/04-dispositivos.md) | Hardware, firmware, protocolo, offline, OTA, seguridad |
| [05 Arquitectura técnica](docs/05-arquitectura-tecnica.md) | Backend .NET, PostgreSQL, multitenancy, frontend, infraestructura |
| [06 Privacidad y legal](docs/06-privacidad-y-legal.md) | Ley 25.326, padrón único, retención, derechos de los titulares |
| [07 Roadmap y MVP](docs/07-roadmap-mvp.md) | Fases, criterios de salida y próximos pasos |
| [08 Plan de cambios y flujo OpenSpec](docs/08-plan-de-cambios-openspec.md) | Cómo desarrollamos con OpenSpec y la lista ordenada de cambios planificados |

## Principios

1. **Un solo modelo, muchos contextos.** Edificio, country y empresa son configuración, no código distinto.
2. **Un solo motor de permisos.** Todo permiso es un *otorgamiento*; gana siempre la denegación.
3. **La puerta decide sola.** Los dispositivos funcionan offline con una política compilada y firmada.
4. **Simple por diseño.** Copiamos el vocabulario de los sistemas grandes (Lenel, Genetec, Salto, Brivo) y dejamos afuera lo que complica sin aportar valor.
5. **Privacidad desde el diseño.** Padrón único cifrado, sin enumeración, retención limitada.
6. **Monolito modular, una base de datos.** Preparado para separar microservicios después.

## Desarrollo local

Requisitos: SDK de .NET 10 (ver `global.json`) y Docker.

```bash
# 1. Levantar PostgreSQL y Mosquitto
docker compose --env-file infra/.env.example -f infra/docker-compose.yml up -d

# 2. Aplicar las migraciones (con el rol owner)
dotnet run --project src/Nexgate.Host -- migrate

# 3. Correr la API (http://localhost:5095)
dotnet run --project src/Nexgate.Host

# 4. Verificar
curl http://localhost:5095/health/live      # el proceso responde
curl http://localhost:5095/health/ready     # la base de datos acepta conexiones

# 5. Pruebas (usan PostgreSQL real)
dotnet test
```

**Roles de base de datos.** `infra/postgres/init/01-roles.sh` crea dos roles al inicializar el volumen:
`nexgate_owner` (migraciones, dueño de las tablas) y `nexgate_app` (la aplicación: sin propiedad y sin poder saltear RLS).
La cadena `ConnectionStrings:Default` usa `nexgate_app` y `ConnectionStrings:Migrations` usa `nexgate_owner`.
El script solo corre al crear el volumen: si ya tenías el volumen de una versión anterior, recrealo con
`docker compose --env-file infra/.env.example -f infra/docker-compose.yml down -v` y volvé a levantarlo.

**Primer uso: operador, organización y administrador.** Toda la API de negocio exige un token. La organización
activa sale del token y solo vale si el usuario es miembro; ningún encabezado la puede fijar.

```bash
# 1. Crear el operador de plataforma (la contraseña nunca va en los argumentos)
NEXGATE_OPERATOR_PASSWORD='<contraseña de 12 o más>' dotnet run --project src/Nexgate.Host -- create-platform-operator --email operador@ejemplo.com

# 2. Iniciar sesión como operador y crear una organización con su primer administrador
curl -X POST http://localhost:5095/api/v1/auth/token -H "Content-Type: application/json" \
  -d '{"email":"operador@ejemplo.com","password":"<contraseña>"}'
curl -X POST http://localhost:5095/api/v1/platform/organizations -H "Content-Type: application/json" \
  -H "Authorization: Bearer <accessToken del operador>" -d '{"name":"Consorcio Demo","administratorEmail":"admin@ejemplo.com"}'

# 3. Aceptar la invitación (el token viene en administratorInvitation.token) e iniciar sesión como administrador
curl -X POST http://localhost:5095/api/v1/invitations/accept -H "Content-Type: application/json" \
  -d '{"token":"<token de la invitación>","password":"<contraseña del administrador>"}'
curl -X POST http://localhost:5095/api/v1/auth/token -H "Content-Type: application/json" \
  -d '{"email":"admin@ejemplo.com","password":"<contraseña del administrador>"}'

# 4. Usar la API con ese token
curl http://localhost:5095/api/v1/sites -H "Authorization: Bearer <accessToken del administrador>"
```

El administrador suma usuarios con `POST /api/v1/invitations` (rol `administrator`, `guard`, `resident` o
`employee`, con alcance `organization` o `site`). Mientras no exista el envío de correos, el token de la invitación se
le devuelve a quien invita para que lo haga llegar. `GET /api/v1/me/permissions` dice qué puede hacer cada usuario.

**Swagger UI y contrato OpenAPI.** En `Development`, la referencia interactiva está en
`http://localhost:5095/swagger` y el documento en `/openapi/v1.json` (fuera de `Development` responden 404). Para probar
un endpoint protegido: ejecutar `signIn` (`POST /api/v1/auth/token`), copiar el `accessToken`, pegarlo en **Authorize** y
llamar la operación; responde igual que a cualquier cliente con ese token. El token no se guarda al recargar la página.

El documento es el contrato del que el frontend genera su cliente (D-32) y está versionado en
`api/nexgate-v1.openapi.json`. Si un cambio en la API lo modifica, `dotnet test` falla hasta regenerarlo:

```bash
NEXGATE_UPDATE_OPENAPI=1 dotnet test tests/Nexgate.Host.Tests --filter OpenApiContract
```

El diff del archivo se revisa con el cambio. Cada endpoint declara nombre de operación (`WithName`), resumen en español,
etiqueta, respuesta de éxito (`Produces`) y errores (`Problems`); los filtros de permisos y de validación declaran solos
sus 400, 401 y 403. Una respuesta que las pruebas producen y el endpoint no declara también hace fallar `dotnet test`.

**Clave de firma de los tokens.** En `Development`, si `Identity:Tokens:SigningKey` está vacía, se genera una clave
efímera al arrancar (los tokens dejan de valer al reiniciar). Fuera de `Development` la aplicación no arranca sin una
clave EC P-256 en PEM, por ejemplo en la variable `Identity__Tokens__SigningKey`
(`openssl ecparam -name prime256v1 -genkey -noout`).

**Pruebas.** Cada corrida crea una base nueva (`nexgate_test_...`), la migra con el rol owner, se conecta con el rol de la
aplicación y la borra al terminar. Las cadenas salen de `src/Nexgate.Host/appsettings.json`, o de las variables
`NEXGATE_TEST_PG_OWNER` (owner) y `NEXGATE_TEST_PG` (aplicación).

**Migraciones nuevas.** `dotnet tool restore` instala `dotnet-ef`. Ejemplo:
`dotnet ef migrations add <Nombre> --project src/Modules/<Modulo>/<Modulo>.Infrastructure --startup-project src/Nexgate.Host --context <Modulo>DbContext --output-dir Migrations`.
Toda tabla de negocio debe llamar a `EnableTenantIsolation` y el schema a `GrantAppAccess`; una prueba lo verifica.
Una función `SECURITY DEFINER` nueva tiene que fijar `search_path` y sumarse a la lista conocida de la prueba de catálogo.

Estructura: `src/Nexgate.Host` (API), `src/Nexgate.Workers` (procesos en segundo plano), `src/BuildingBlocks` (piezas compartidas),
`src/Modules/<Modulo>` (un proyecto por capa: Contracts, Domain, Application, Infrastructure), `tests/`, `infra/`.
Módulos: `Tenancy` (organizaciones y sitios) e `Identity` (cuentas, inicio de sesión, membresías, invitaciones y roles).

## Desarrollo con OpenSpec

Todo el desarrollo pasa por [OpenSpec](https://github.com/Fission-AI/OpenSpec): primero se acuerda el cambio (propuesta, specs, diseño y tareas) y después se implementa.

- `openspec/specs/`: comportamiento acordado (fuente de verdad). `openspec/changes/`: cambios en curso.
- `openspec/config.yaml`: contexto y reglas del proyecto para los artefactos.
- Ver el plan completo de cambios en [08](docs/08-plan-de-cambios-openspec.md).

La CLI es opcional para consultar el estado (`npm install -g @fission-ai/openspec@latest`, luego `openspec list` y `openspec view`).
