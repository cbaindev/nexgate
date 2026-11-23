import i18n from 'i18next'
import { initReactI18next } from 'react-i18next'
import es from './es.json'

// Interfaz en español desde un catálogo (design de add-admin-panel-shell, decisión 1). Otro idioma = otro catálogo.
void i18n.use(initReactI18next).init({
  resources: { es: { translation: es } },
  lng: 'es',
  fallbackLng: 'es',
  interpolation: { escapeValue: false },
  returnNull: false,
})

export default i18n
