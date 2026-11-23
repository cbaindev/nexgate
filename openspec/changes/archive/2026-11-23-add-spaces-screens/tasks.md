# Tasks

## 1. Base

- [x] 1.1 `hasPermissionOnSpace` en `session.ts` (organización, sitio, espacio o ancestro); verificar con pruebas unitarias de los casos de alcance
- [x] 1.2 Componentes `tabs` (`@radix-ui/react-tabs` con versión fija), `confirm-dialog` y `table`, sumados a `/_design`; verificar con `npm run check` y en el navegador
- [x] 1.3 API simulada con espacios, puertas, camino de acceso, conflictos 409 y permisos con alcance de espacio; verificar con las pruebas existentes en verde

## 2. Selector de sitio

- [x] 2.1 `SitePicker` y `useSelectedSite` con el sitio en `?sitio=`, elección automática con un solo sitio, sitio no disponible y organización sin sitios; verificar con pruebas de los escenarios de `Site selection`

## 3. Espacios

- [x] 3.1 Pantalla `/espacios` con pestañas en la dirección y la sección marcada como construida; verificar con las pruebas de navegación
- [x] 3.2 Árbol plegable con íconos por tipo, insignia de área común, filtro por nombre y espacio elegido en la dirección; verificar con pruebas de `Show the tree` y `Filter by name`
- [x] 3.3 Detalle con puertas que entran y camino de acceso; verificar con la prueba de `Details and access path`
- [x] 3.4 Alta de la raíz y de hijos, edición y eliminación con confirmación, errores 400 y 409 en su lugar y acciones según el permiso por espacio; verificar con pruebas de los demás escenarios de `Space tree screen`

## 4. Puertas

- [x] 4.1 Lista de puertas (tabla en escritorio, tarjetas en celular) con alta, edición y eliminación, modo de falla sin valor inicial y desplegables con la ruta del espacio; verificar con pruebas de los escenarios de `Access points screen`

## 5. Verificación y documentación

- [x] 5.1 Revisión en el navegador con la API real como administrador y como guardia: armar un edificio con pisos, unidades y puertas, ver el camino de acceso, provocar un 409, en claro y oscuro, escritorio y celular, con teclado
- [x] 5.2 README de la SPA (selector de sitio y permiso por espacio) y `docs/08` (7b implementado); verificar releyendo contra los specs
- [x] 5.3 Verificación final: `npm run check` en verde, y `dotnet build` y `dotnet test` en verde (sin cambios en el backend)
