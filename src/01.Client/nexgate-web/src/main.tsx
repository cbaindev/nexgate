import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { BrowserRouter } from 'react-router'
import { App } from '@/app/App'
import { UpdatePrompt } from '@/app/UpdatePrompt'
import '@/i18n'
import { initTheme } from '@/theme/theme'
import './index.css'

initTheme()

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <BrowserRouter>
      <App />
      <UpdatePrompt />
    </BrowserRouter>
  </StrictMode>,
)
