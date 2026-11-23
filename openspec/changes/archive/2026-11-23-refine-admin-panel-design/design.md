# Design

## Context

La SPA de `add-admin-panel-shell` usa Tailwind 4 con los tokens de shadcn/ui en `src/index.css` (paleta teal, `prefers-color-scheme` para el modo oscuro, fuente del sistema), cuatro componentes en `src/components/ui` (botón, tarjeta y aviso, menú desplegable, campos de formulario) y un marco con barra superior, barra lateral y menú inferior en celular (`src/app/Layout.tsx`). Las pantallas son inicio de sesión, elección de organización, inicio, sitios y las páginas de mensaje. El lint prohíbe `localStorage`, `sessionStorage` y `document.cookie`. Decisiones del usuario: **paleta azul índigo**, **Inter**, **modo oscuro del sistema con selector** y **barra lateral fija**.

## Goals / Non-Goals

**Goals**
- Un sistema de diseño chico y coherente que las pantallas de 7b usen sin decidir colores ni espaciados por su cuenta.
- El mismo comportamiento que hoy: este cambio no toca la sesión, los permisos ni la API.
- Accesibilidad AA en los dos temas.

**Non-Goals**
- Marca blanca por organización, barra lateral plegable, gráficos y regresión visual automatizada.

## Decisions

### 1. Paleta y tokens

- Tokens en `oklch` en `src/index.css`, con los nombres de shadcn/ui (`--primary`, `--muted`, `--border`, `--ring`, …) más:
  - `--success`, `--warning`, `--info` (y sus `-foreground`) para insignias y avisos.
  - `--sidebar`, `--sidebar-foreground`, `--sidebar-accent` y `--sidebar-border` para la barra lateral.
  - `--shadow-xs` a `--shadow-lg` y radios `--radius-sm` a `--radius-xl` derivados de `--radius`.
- Primario índigo (cerca de `oklch(0.51 0.2 275)` en claro y `oklch(0.68 0.16 275)` en oscuro), acento cian para detalles (el punto del logo, foco de gráficos futuros), neutros fríos (tono 265).
- El oscuro no es un gris plano: fondo azul muy oscuro y superficies con un escalón de luminosidad cada una (fondo, tarjeta, elevado).
- Una prueba calcula el contraste de los pares de tokens texto/fondo de los dos temas (desde el propio `index.css`) y falla por debajo de 4.5:1 en texto y de 3:1 en el indicador de foco. Los bordes de los campos quedan fuera: a 3:1 se ven pesados, y el campo se identifica igual por su etiqueta y su fondo. Así el AA no depende de revisarlo a ojo.
- **Alternativa descartada:** la paleta de Tailwind (`indigo-600`, …) usada directamente en las clases. Ata cada pantalla al color y hace imposible el tema oscuro coherente o una marca blanca futura.

### 2. Tipografía

- Inter variable empaquetada con `@fontsource-variable/inter` (versión fija): Vite copia los `woff2` al build. El service worker precarga solo los subconjuntos latino y latino extendido; los demás (cirílico, griego, vietnamita) se descargan del mismo origen solo si una página los usa. Ningún pedido a otro origen.
- `font-feature-settings` con `cv11` y `tnum` en tablas y números (identificadores, horas), para que las columnas alineen.
- Escala: títulos de página `text-2xl font-semibold tracking-tight`, títulos de tarjeta `text-base font-semibold`, texto `text-sm`.
- **Alternativa descartada:** Google Fonts por CDN. Pedido a un tercero en cada carga (privacidad) y sin la fuente cuando no hay red.

### 3. Tema claro, oscuro o del sistema

- Tailwind 4 con variante `dark` por clase (`@custom-variant dark (&:where(.dark, .dark *))`): la clase `dark` en `<html>` decide el tema y los tokens oscuros cuelgan de `.dark`.
- `src/theme/theme.ts`: lee y guarda la preferencia (`light`, `dark` o `system`) en `localStorage` con la clave `nexgate.theme`, escucha `matchMedia('(prefers-color-scheme: dark)')` cuando la preferencia es `system`, y actualiza la clase y el `<meta name="theme-color">`. Todo acceso al almacenamiento va en `try/catch`: si el navegador lo bloquea, el tema sigue al sistema.
- `public/theme-init.js`, un script mínimo y bloqueante en el `<head>` de `index.html`, aplica la clase antes de cargar el bundle, para que no haya destello. Lee la misma clave y solo acepta los valores conocidos. Es un archivo del mismo origen y no un script en línea, así una CSP futura no necesita `unsafe-inline` ni un hash.
- Lint: `src/theme/theme.ts` es el único archivo con excepción a la regla de almacenamiento, con un comentario que explica por qué (es una preferencia de presentación, no un dato de sesión). Una prueba verifica que tras iniciar sesión y elegir tema el almacenamiento tiene solo esa clave.
- El selector vive en el menú de usuario como grupo de opciones (claro, oscuro, sistema) y en la pantalla de inicio de sesión como botón de ícono.
- **Alternativa descartada:** guardar la preferencia en el servidor, en la cuenta. Requiere cambiar la API y no aplica antes de iniciar sesión ni antes del primer pintado.
- **Alternativa descartada:** cookie. Viaja en cada pedido a la API sin necesidad.

### 4. Marco de la aplicación

- Barra lateral fija de 16 rem en `md` y más: logo arriba, bloque de la organización activa (nombre y, si hay varias, botón para cambiar), secciones agrupadas en "Operación" (sitios, espacios y puertas, accesos, dispositivos, eventos) y "Administración" (miembros, plataforma), y abajo el usuario. La sección activa lleva fondo de acento y una barra indicadora a la izquierda.
- Los grupos se agregan a `sections.ts` (campo `group`): la tabla sigue siendo la única fuente de navegación, rutas y permisos.
- Encabezado del área de contenido con la ubicación (organización › sección, desde la misma tabla) y el menú de usuario con avatar de iniciales. El título de la página es el `h1` de cada pantalla (`page-header`), así no hay dos encabezados con el mismo nombre.
- Enlace "Ir al contenido" para teclado y transición de entrada del contenido al cambiar de sección.
- Celular: menú inferior con ícono y etiqueta, indicador de sección activa y área segura; el menú de usuario queda en el encabezado.
- Contenido con ancho máximo de lectura (`max-w-6xl`) y márgenes consistentes.
- **Alternativa descartada:** barra lateral plegable (el usuario eligió fija). Se puede agregar después sin cambiar la tabla de secciones.

### 5. Componentes

En `src/components/ui`, al estilo shadcn/ui (código copiado y adaptado, Radix por debajo):

- Ajustados: `button` (variantes `default`, `secondary`, `outline`, `ghost`, `destructive`, `link`; tamaños; estado de carga con indicador), `card`, `alert` (variantes con ícono: info, éxito, advertencia, peligro), `form` (campo con etiqueta, ayuda y error).
- Nuevos: `badge`, `skeleton`, `avatar` (iniciales, sin Radix), `sheet` (panel lateral sobre `@radix-ui/react-dialog`, para formularios), `separator`, `empty-state` (ilustración, título, texto y acción) y `page-header` (título, descripción y acciones). El menú desplegable suma opciones de radio para el tema.
- Las ilustraciones de los estados vacíos son SVG en línea con los tokens de color (se adaptan al tema), no imágenes.
- `/_design` es una ruta solo de desarrollo (`import.meta.env.DEV`) que muestra todos los componentes y la paleta en los dos temas; el build de producción no la incluye.
- **Alternativa descartada:** otra biblioteca de componentes (Mantine, MUI). Ya se eligió shadcn/ui en el cambio 7 y cambiarla duplicaría el trabajo.

### 6. Pantallas

- Inicio de sesión: dos columnas en pantallas grandes (panel de marca con degradado índigo, logo y una frase del producto; formulario a la derecha), una columna en celular. Botón con estado de carga y mostrar u ocultar contraseña.
- Elección de organización: lista de tarjetas con inicial de la organización.
- Inicio: saludo, organización activa y tarjetas de acceso rápido a las secciones permitidas (de la tabla de secciones).
- Sitios: `page-header` con la acción, tarjetas con ícono según el tipo, insignia del tipo y zona horaria; alta y edición en un `sheet`. Esqueletos al cargar y `empty-state` sin sitios.
- Páginas de mensaje: `empty-state` con ilustración.
- Los textos nuevos van a `es.json`; los nombres accesibles existentes no cambian, así las pruebas actuales siguen valiendo.

### 7. Movimiento

- Animaciones con `@keyframes` propias en `index.css`, expuestas como utilidades de Tailwind (`animate-enter`, `animate-sheet-in`, `animate-rise`, …): aparición de `sheet`, menús, avisos y contenido, transición de color en la navegación. Duraciones de 120 a 260 ms.
- `@media (prefers-reduced-motion: reduce)` anula animaciones y transiciones de forma global.
- **Alternativa descartada:** Framer Motion. Agrega peso al bundle para animaciones que CSS resuelve.

### 8. Logo e íconos

- Isotipo: una puerta estilizada con un punto cian (el "pase" o la cerradura) sobre un cuadrado redondeado índigo; logotipo "nexgate" en Inter semibold. SVG fuente en `public/brand/`.
- Los PNG de la PWA (`icon-192`, `icon-512`, `icon-maskable-512`, `apple-touch-icon`) se regeneran desde el SVG con un script (`scripts/icons.mjs`, con `sharp` como dependencia de desarrollo) y se versionan.
- Manifiesto: `theme_color` y `background_color` de la marca.

## Risks / Trade-offs

- **Excepción del lint para el tema** → acotada a un archivo y una clave, con prueba de que no se guarda nada más.
- **Script bloqueante en el `<head>`** → es chico (menos de 1 KB) y del mismo origen; el service worker lo precarga.
- **Rediseño que rompe pruebas** → se conservan nombres accesibles y textos; las pruebas que cambien lo hacen por un comportamiento del spec nuevo, no por estilo.
- **Peso del bundle** (fuente y Radix Dialog) → el JavaScript pasó de 452 KB a 486 KB (146 KB a 155 KB comprimido). La precarga suma Inter latino y latino extendido (unos 133 KB).

## Migration Plan

Sin migración de datos. El cambio es solo de la SPA y se despliega con su build. Los usuarios con la PWA instalada reciben el aviso de actualización que ya existe.

## Open Questions

Ninguna.
