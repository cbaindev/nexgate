import js from '@eslint/js'
import reactHooks from 'eslint-plugin-react-hooks'
import globals from 'globals'
import tseslint from 'typescript-eslint'

export default tseslint.config(
  { ignores: ['dist', 'dev-dist', 'node_modules', 'src/api/schema.d.ts'] },
  {
    files: ['**/*.{ts,tsx}'],
    extends: [js.configs.recommended, ...tseslint.configs.recommended],
    languageOptions: {
      ecmaVersion: 2022,
      globals: globals.browser,
    },
    plugins: { 'react-hooks': reactHooks },
    rules: {
      ...reactHooks.configs.recommended.rules,
      // Nada sensible en el almacenamiento del navegador (design, decisión 3).
      'no-restricted-globals': ['error', { name: 'localStorage', message: 'No guardar datos de sesión en el navegador.' },
        { name: 'sessionStorage', message: 'No guardar datos de sesión en el navegador.' }],
      'no-restricted-properties': ['error',
        { object: 'window', property: 'localStorage', message: 'No guardar datos de sesión en el navegador.' },
        { object: 'window', property: 'sessionStorage', message: 'No guardar datos de sesión en el navegador.' },
        { object: 'document', property: 'cookie', message: 'La cookie de renovación es httpOnly: no se toca desde el código.' }],
    },
  },
  {
    // La preferencia de tema es lo único que se guarda en el navegador: es de presentación, no de sesión
    // (design de refine-admin-panel-design, decisión 3).
    files: ['src/theme/theme.ts'],
    rules: { 'no-restricted-globals': 'off' },
  },
  {
    // Las pruebas leen el almacenamiento para verificar que queda vacío.
    files: ['src/test/**'],
    rules: { 'no-restricted-globals': 'off', 'no-restricted-properties': 'off' },
  },
  {
    files: ['scripts/**/*.mjs', '*.js'],
    extends: [js.configs.recommended],
    languageOptions: { globals: globals.node },
  },
)
