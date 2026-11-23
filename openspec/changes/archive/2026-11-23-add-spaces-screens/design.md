# Design

## Context

La SPA tiene el marco, la sesión, la navegación por permisos y la pantalla de sitios, con el diseño de `refine-admin-panel-design` (tokens índigo, `page-header`, `sheet`, `empty-state`, `skeleton`, `badge`). La API ofrece:

- `GET/POST /sites/{siteId}/spaces`.
- `GET/PUT/DELETE /spaces/{id}`.
- `GET /spaces/{id}/access-path`.
- `GET/POST /sites/{siteId}/access-points`.
- `GET/PUT/DELETE /access-points/{id}`.

Los espacios traen `parentId`, `depth` y `ancestorIds`. Los permisos de `/me/permissions` llegan con su alcance: toda la organización, una lista de sitios y una lista de espacios con su sitio. Espacios y puertas se manejan con el mismo permiso: `spaces.read` para leer y `spaces.manage` para modificar. Los tipos de espacio son `site`, `building`, `floor`, `unit`, `common_area`, `parking`, `lot` y `zone`. Los de puerta son `door`, `gate`, `barrier`, `turnstile` y `elevator`. Los modos de falla son `fail_safe` y `fail_secure`.

## Goals / Non-Goals

**Goals**
- Que un administrador arme la estructura completa de un sitio desde la SPA.
- Dejar el selector de sitio listo para 7d y 7e.
- Que las acciones visibles coincidan con lo que el servidor va a permitir, incluido el alcance por espacio.

**Non-Goals**
- Mover espacios, alta masiva, vista gráfica y simulación de acceso.

## Decisions

### 1. Selector de sitio en la dirección

- Componente `SitePicker` en `src/components/site-picker` y un hook `useSelectedSite()`:
  - leen los sitios visibles (la misma consulta de la pantalla de sitios, así no hay un pedido extra);
  - toman el sitio de `?sitio=<id>`;
  - si hay un solo sitio visible, lo eligen y lo escriben en la dirección con `replace`.
- Va en el `page-header` de cada sección por sitio, como un desplegable con el nombre y el tipo del sitio.
- Un `sitio` que no está entre los visibles muestra "Este sitio no está disponible" con el selector. No se distingue entre "no existe" y "es de otra organización", igual que la API.
- **Alternativa descartada:** recordar el sitio en el almacenamiento del navegador. El lint lo prohíbe fuera del tema, y la dirección ya resuelve recargas y enlaces.
- **Alternativa descartada:** el sitio en la ruta (`/espacios/:siteId`). Obliga a duplicar rutas en la tabla de secciones; el parámetro de consulta deja una sola ruta por sección.

### 2. Pantalla con dos pestañas

- `/espacios` muestra `page-header` con el selector y pestañas "Espacios" y "Puertas", con Radix Tabs: teclado con flechas y `aria-selected`. La pestaña activa también va en la dirección (`?vista=puertas`), así recargar no la pierde.
- **Espacios**, en dos columnas en escritorio (árbol a la izquierda, detalle a la derecha) y una en celular, donde el detalle va debajo del árbol y la página se desplaza hasta él al elegir un espacio. Un `sheet` para el detalle taparía el árbol y dejaría dos paneles laterales apilados al editar.
- **Puertas**: un solo marcado de tabla. En escritorio se ve como tabla; en celular cada fila es una tarjeta, con la etiqueta de cada dato delante (`data-label`). Así no hay dos listas en el DOM, cosa que duplicaría lo que oyen los lectores de pantalla.
- **Alternativa descartada:** dos secciones separadas en la barra lateral. La tabla de secciones ya tiene una sola entrada "Espacios y puertas", y las puertas se entienden mirando el árbol.

### 3. Árbol

- Se arma en el cliente desde la lista plana: un `Map` de `parentId` a hijos, ordenados por nombre. Con miles de espacios sigue siendo instantáneo; la API ya devuelve todo el sitio en un pedido.
- Listas anidadas con un botón de despliegue por nodo (`aria-expanded`, `aria-controls`) y el nombre como botón de selección (`aria-current` en el elegido). No es un `role="tree"` completo: el patrón de disclosure es accesible, más simple de probar y suficiente para este tamaño.
- Al abrir, se despliegan la raíz, su primer nivel y los ancestros del espacio elegido; el resto empieza plegado. Al elegir otro espacio se despliegan sus ancestros, y lo que ya estaba desplegado no se pliega.
- El ícono de cada nodo lleva el nombre del tipo como texto accesible ("Unidad 5B").
- Filtro por nombre, sin distinguir mayúsculas ni tildes: muestra las coincidencias y sus ancestros (desde `ancestorIds`), despliega lo necesario y resalta el texto que coincide.
- Cada nodo lleva el ícono del tipo, su nombre y la insignia "Área común" si corresponde. En el detalle, la cantidad de hijos y de puertas que entran.
- El elegido va en la dirección (`?espacio=<id>`).

### 4. Detalle del espacio

- Tipo, área común, espacio padre (enlace) y acciones: agregar hijo, editar y eliminar.
- **"Puertas que entran"**: las puertas del sitio cuyo `toSpaceId` es el espacio. Sale de la lista de puertas ya cargada, sin un pedido extra.
- **"Camino de acceso"**: `GET /spaces/{id}/access-path`, mostrado como una secuencia vertical numerada:
  - empieza en "Afuera" y sigue con cada puerta, su tipo y el espacio al que da;
  - si viene vacío, explica que ninguna puerta da acceso a ese espacio ni a los que lo contienen.
- **Alternativa descartada:** calcular el camino en el cliente. Duplicaría una regla de la API (el orden por profundidad y nombre) que ya tiene spec y pruebas.

### 5. Permiso por espacio

- `hasPermissionOnSpace(session, permission, space)` en `session.ts`. Es verdadero si el permiso alcanza a:
  - toda la organización;
  - el sitio del espacio;
  - el espacio mismo, o cualquiera de sus `ancestorIds`.
- Reglas para mostrar acciones:
  - **Crear la raíz:** permiso a nivel de organización o de sitio.
  - **Agregar un hijo, editar o eliminar un espacio:** permiso sobre ese espacio.
  - **Crear, editar o eliminar una puerta:** permiso sobre su espacio de destino. El origen puede estar fuera del alcance, como dice el spec `access-points` ("Origin outside the subtree"). En el formulario, el destino ofrece solo espacios que el usuario puede manejar, y el origen, cualquiera del sitio o "Afuera".
- Las pestañas y la lectura dependen de `spaces.read`, igual que la navegación.
- Es solo presentación: si el servidor rechaza con 403, se muestra el mensaje de permiso como hoy.

### 6. Formularios y confirmaciones

- Alta y edición en `sheet`, como en sitios:
  - **Espacio:** nombre, tipo y "Es un área común". El padre se muestra como texto y no se puede cambiar. La raíz solo admite el tipo `site`; los hijos, todos los demás. Hay una sola raíz por sitio, así que "Crear el espacio raíz" solo aparece en un sitio sin espacios. "Agregar hijo" no aparece a 10 niveles de la raíz, el máximo de la API. El tipo del hijo se sugiere según el padre (sitio → edificio, edificio → piso, piso → unidad; en los demás, zona) y se puede cambiar.
  - **Puerta:** nombre, tipo, "Da a" (destino), "Se entra desde" (origen u "Afuera") y modo de falla, en radios con una explicación cada uno y sin valor inicial:
    - "Se abre sin energía (fail-safe)";
    - "Queda cerrada sin energía (fail-secure)".
- Los desplegables de espacios muestran la ruta completa ("Torre A › Piso 5 › 5B"), así no se confunden nombres repetidos en ramas distintas.
- Eliminar pasa por `confirm-dialog` (Radix Dialog con `role="alertdialog"`): nombra lo que se elimina, el foco empieza en "Cancelar" y el botón de confirmar usa la variante `destructive`.
- Errores:
  - **400:** junto a cada campo.
  - **409:** aviso dentro del formulario o del diálogo, con el `title` del servidor, sin cerrarlo ni perder lo cargado.
  - **403 y 404:** con los mensajes existentes.
- Tras crear, editar o eliminar, se invalidan las consultas de espacios y puertas del sitio con TanStack Query. El espacio nuevo queda elegido y su padre desplegado.

### 7. Componentes y textos

- `tabs` (Radix Tabs, versión fija), `confirm-dialog` (sobre el `Dialog` de Radix que ya está) y `table` (marcado semántico con los estilos de los tokens). Los tres se suman a `/_design`.
- Íconos por tipo en un solo mapa, `src/features/spaces/types.ts`, junto con las claves del catálogo:
  - espacios: `site`, `building`, `floor`, `unit`, `common_area`, `parking`, `lot`, `zone`;
  - puertas: `door`, `gate`, `barrier`, `turnstile`, `elevator`.
- Los textos van a `es.json` en `spaces.*`. La prueba del catálogo cubre las claves dinámicas de tipos, como ya lo hace con los tipos de sitio.

### 8. Pruebas

- La API simulada suma espacios y puertas en memoria:
  - con las reglas mínimas que la pantalla necesita mostrar (409 por nombre repetido entre hermanos, por espacio con hijos o puertas y por puerta asignada a un dispositivo);
  - con el camino de acceso calculado por profundidad y nombre;
  - y con permisos con alcance de espacio en `/me/permissions`.
- Una prueba por escenario del spec, con Testing Library y los nombres accesibles.
- Revisión en el navegador contra la API real con los datos de desarrollo.

## Risks / Trade-offs

- **Sitios muy grandes** (barrios con cientos de lotes) → el árbol empieza plegado y el filtro resuelve la búsqueda. Si hiciera falta, la virtualización se agrega después sin cambiar el spec.
- **La regla del permiso por espacio vive también en el cliente** → es solo presentación, con pruebas contra los mismos casos del spec `role-scopes`; el servidor sigue siendo la autoridad.
- **Peso del bundle** → el JavaScript pasó de 486 KB a 534 KB, por Radix Tabs y las pantallas. Si las secciones siguientes lo hacen crecer mucho, se cargan por sección con `lazy`.
- **Lista de puertas y camino como pedidos separados** → la lista se pide una vez por sitio, y el camino solo al elegir un espacio y queda en caché mientras no cambie nada.

## Migration Plan

Sin migración. Solo la SPA cambia y se despliega con su build.

## Open Questions

Ninguna.
