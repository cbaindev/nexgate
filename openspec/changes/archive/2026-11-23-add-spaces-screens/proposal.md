# Proposal

## Why

La API ya maneja el árbol de espacios, las puertas y el camino de acceso a cada espacio (`add-spaces-and-access-points` y `add-space-scoped-roles`), pero en la SPA la sección "Espacios y puertas" dice "Próximamente". Sin esa pantalla, un administrador no puede armar la estructura de un sitio (edificio, pisos, unidades, áreas comunes) ni sus puertas sin llamar a la API a mano. Es el cambio 7b de `docs/08`. El panel mínimo de la fase 1 se partió en cuatro cambios (7b espacios y puertas, 7d accesos, 7e dispositivos y eventos, 7f miembros) para revisarlos por separado. Este es el primero porque los otros tres trabajan sobre espacios y puertas.

## What Changes

- **Selector de sitio** común a las secciones que trabajan dentro de un sitio (espacios y puertas ahora; accesos, dispositivos y eventos después):
  - Muestra los sitios visibles de la organización activa y elige solo cuando hay uno.
  - El sitio elegido va en la dirección (`?sitio=`), así un enlace o una recarga abren el mismo sitio.
- **Pantalla de espacios y puertas** con dos pestañas:
  - **Espacios**:
    - El árbol del sitio, plegable, con ícono según el tipo, insignia de área común y un filtro por nombre.
    - Al elegir un espacio, un panel con su detalle, las puertas que entran a él y el camino de acceso desde afuera, en orden.
    - Crear el espacio raíz, agregar un hijo, renombrar, cambiar tipo o área común, y eliminar con confirmación.
  - **Puertas**:
    - La lista de puertas del sitio con su tipo, el espacio al que dan, desde dónde se entra y su modo de falla.
    - Crear, modificar y eliminar puertas, con confirmación al eliminar.
- **Permisos con alcance**: las acciones aparecen solo donde el usuario puede hacerlas. Un administrador de un edificio ve todo el sitio, pero solo puede modificar dentro de su edificio. Un guardia ve sin acciones. El servidor sigue decidiendo cada operación.
- **Errores del servidor en su lugar**: los conflictos (nombre repetido entre hermanos, espacio con hijos o puertas, espacio usado por un rol o un otorgamiento, puerta controlada por un dispositivo) se muestran con el mensaje del servidor, sin perder lo cargado en el formulario.
- **Componentes nuevos** del sistema de diseño: pestañas, diálogo de confirmación y tabla simple.

Fuera de alcance:
- Las pantallas de 7d, 7e y 7f: accesos, dispositivos, eventos y miembros, aunque usarán el mismo selector de sitio.
- Mover un espacio a otro padre: la API no lo permite.
- Plano o vista gráfica del edificio.
- Alta masiva de espacios, por ejemplo "pisos 1 a 20 con 4 unidades cada uno", o importación desde planilla.
- La simulación de acceso en una puerta, que es de 7d porque necesita sujetos y credenciales.
- Cambios en la API o en el contrato OpenAPI.

## Capabilities

### New Capabilities

Ninguna.

### Modified Capabilities

- `admin-shell`: suma el selector de sitio y la pantalla de espacios y puertas.

## Impact

- **SPA** (`src/01.Client/nexgate-web`):
  - `src/features/spaces/` es nueva (árbol, detalle, puertas y formularios).
  - `src/components/site-picker` es nuevo.
  - En `src/components/ui` se suman `tabs`, `confirm-dialog` y `table`.
  - `sections.ts` marca la sección como construida.
  - `session.ts` suma la verificación de permiso por espacio.
  - `es.json` suma los textos nuevos.
  - Las pruebas usan la API simulada con espacios y puertas.
- **Dependencias**: `@radix-ui/react-tabs`, con versión fija.
- **API, base de datos y contrato**: sin cambios.
- **Documentación**: `docs/08` y el README de la SPA.
