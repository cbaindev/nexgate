# Proposal

## Why

La API ya maneja quién puede pasar por dónde: sujetos, horarios, credenciales, otorgamientos y la simulación de una decisión en una puerta (`add-access-evaluation` y anteriores). Pero en la SPA la sección "Accesos" dice "Próximamente". Sin esa pantalla, un administrador no puede dar de alta a una persona, asignarle una tarjeta o un PIN, darle permiso a su unidad en un horario ni comprobar si va a poder pasar, sin llamar a la API a mano. Es el cambio 7d de `docs/08`. Va después de 7b porque usa su selector de sitio y el árbol de espacios y puertas.

## What Changes

- **Pantalla de accesos centrada en la persona** (decisión del usuario), con tres pestañas y el selector de sitio de 7b:
  - **Personas**:
    - La lista de sujetos del sitio, con su tipo (persona o anónimo) y un filtro por alias.
    - Al elegir uno, su detalle con **credenciales** y **permisos**:
      - **Credenciales:** tipo, vigencia y estado (vigente, por empezar, vencida); alta, cambio de vigencia y baja.
      - **Permisos:** permitir o denegar, a qué espacio o puerta, horario, vigencia y usos restantes; alta y baja.
    - Alta, edición y baja de sujetos, con confirmación al eliminar.
  - **Horarios**:
    - La lista de horarios del sitio, cada uno con sus ventanas en palabras ("Lunes a viernes, 08:00 a 17:00").
    - Alta, edición y baja, con un editor de ventanas por días y horas.
  - **Probar acceso**: se elige una puerta, el tipo y el valor de la credencial, y el momento (ahora o una fecha y hora del sitio). Muestra si pasaría y por qué, en palabras.
- **Probar acceso desde cada puerta** (decisión del usuario): en "Espacios y puertas", cada puerta ofrece "Probar acceso" a quien puede leer accesos. Abre la misma prueba con la puerta ya elegida.
- **El valor de una credencial no se vuelve a mostrar**:
  - Se escribe una sola vez al darla de alta (oculto si es un PIN) y se descarta al guardar.
  - La API no lo devuelve, y la SPA no lo guarda en la dirección, en el navegador ni en los registros. Lo mismo vale para el valor que se prueba en la simulación.
- **Sin datos personales en los sujetos**: el alias identifica a la persona dentro del sitio. El formulario pide no cargar documentos, teléfonos ni correos, como exige el spec `subjects`.
- **Fechas y horas en la zona horaria del sitio**: las vigencias y el momento de la prueba se cargan y se muestran en la hora local del sitio, no en la del navegador.
- **Permisos**: las acciones aparecen solo con el permiso de gestionar accesos sobre el sitio entero. El selector ofrece solo los sitios que el permiso de leer accesos cubre entero, porque un alcance de espacio no da acceso a estos datos (spec `subjects`). Un guardia ve todo y puede probar accesos, pero no modificar.
- **Errores del servidor en su lugar**:
  - Credencial repetida en el sitio.
  - Horario con nombre repetido o con ventanas inválidas.
  - Horario usado por un permiso.
  - Sujeto con credenciales o permisos.

Fuera de alcance:
- Personas del padrón y su documento: el módulo People llega después y los datos de negocio solo referencian el sujeto.
- Invitaciones, accesos temporales con QR y residentes: son cambios posteriores del plan.
- Editar un permiso: la API solo permite crearlo y eliminarlo, así que se reemplaza.
- Ver la política compilada de una puerta.
- Listados de todos los permisos o credenciales del sitio a la vez.
- Importar personas o credenciales en lote.
- Cambios en la API o en el contrato OpenAPI.

## Capabilities

### New Capabilities

Ninguna.

### Modified Capabilities

- `admin-shell`: suma la pantalla de accesos (personas con credenciales y permisos, horarios y prueba de acceso) y la prueba de acceso desde cada puerta. El selector de sitio pasa a filtrar los sitios por permiso.

## Impact

- **SPA** (`src/01.Client/nexgate-web`):
  - `src/features/access/` es nueva (personas, credenciales, permisos, horarios y prueba de acceso).
  - El selector de sitio filtra por permiso.
  - La tabla de puertas suma "Probar acceso".
  - `src/lib/zoned-time.ts` es nuevo: hora local del sitio, con `Intl` y sin dependencias.
  - `sections.ts` marca la sección como construida.
  - `es.json` suma los textos nuevos.
  - La API simulada suma accesos y pruebas.
- **Dependencias**: ninguna nueva.
- **API, base de datos y contrato**: sin cambios.
- **Documentación**: `docs/08` y el README de la SPA.
