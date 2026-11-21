#!/bin/sh
# Crea los roles de Nexgate al inicializar el volumen de PostgreSQL (solo la primera vez).
#   nexgate_owner: dueño de schemas y tablas; corre las migraciones. Puede crear bases (lo usan las pruebas).
#   nexgate_app:   conexión de la aplicación. No es dueño de nada y no puede saltear RLS.
# Ver openspec/changes/add-tenancy-and-isolation/design.md (decisión 1).
set -eu

psql -v ON_ERROR_STOP=1 \
     -v owner_password="${NEXGATE_OWNER_PASSWORD:-nexgate_owner_dev}" \
     -v app_password="${NEXGATE_APP_PASSWORD:-nexgate_app_dev}" \
     --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" <<'SQL'
CREATE ROLE nexgate_owner LOGIN NOSUPERUSER CREATEDB NOCREATEROLE NOBYPASSRLS PASSWORD :'owner_password';
CREATE ROLE nexgate_app LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOBYPASSRLS NOINHERIT PASSWORD :'app_password';
SELECT format('ALTER DATABASE %I OWNER TO nexgate_owner', current_database()) \gexec
SQL
