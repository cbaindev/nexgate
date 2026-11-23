import { Fingerprint, ShieldCheck, WifiOff } from 'lucide-react'
import type { ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { Logo } from '@/components/brand/Logo'
import { ThemeButton } from './ThemeMenu'

/**
 * Marco de las pantallas sin sesión (inicio de sesión y elección de organización): panel de la marca a la izquierda en
 * pantallas grandes y el contenido a la derecha (design de refine-admin-panel-design, decisión 6).
 */
export function AuthLayout({ children }: { children: ReactNode }) {
  const { t } = useTranslation()
  const features = [
    { icon: Fingerprint, text: t('auth.features.permissions') },
    { icon: WifiOff, text: t('auth.features.offline') },
    { icon: ShieldCheck, text: t('auth.features.isolation') },
  ]

  return (
    <div className="grid min-h-dvh lg:grid-cols-[minmax(0,1fr)_minmax(0,1.1fr)]">
      <aside className="relative hidden overflow-hidden bg-brand p-10 text-white lg:flex lg:flex-col">
        <BrandBackdrop />
        <Logo inverted className="relative" />
        <div className="relative mt-auto max-w-md space-y-8">
          <p className="text-3xl leading-tight font-semibold tracking-tight text-balance">{t('auth.headline')}</p>
          <ul className="space-y-4">
            {features.map(({ icon: Icon, text }) => (
              <li key={text} className="flex items-center gap-3 text-[15px] text-white/90">
                <span className="flex size-9 shrink-0 items-center justify-center rounded-lg bg-white/12 ring-1 ring-white/20">
                  <Icon className="size-4.5" aria-hidden="true" />
                </span>
                {text}
              </li>
            ))}
          </ul>
        </div>
        <p className="relative mt-12 text-sm text-white/70">{t('app.tagline')}</p>
      </aside>

      <main className="relative flex flex-col px-4 py-6 sm:px-8">
        <div className="flex items-center justify-between">
          <Logo className="lg:invisible" />
          <ThemeButton />
        </div>
        <div className="flex flex-1 items-center justify-center py-10">
          <div className="w-full max-w-sm animate-rise">{children}</div>
        </div>
      </main>
    </div>
  )
}

/** Fondo del panel de la marca: degradado, cuadrícula sutil y halos, todo con SVG y CSS (sin imágenes externas). */
function BrandBackdrop() {
  return (
    <div aria-hidden="true" className="absolute inset-0">
      <div className="absolute inset-0 bg-[radial-gradient(120%_80%_at_0%_0%,oklch(0.6_0.2_280)_0%,transparent_60%),radial-gradient(90%_70%_at_100%_100%,oklch(0.38_0.17_270)_0%,transparent_70%)]" />
      <svg className="absolute inset-0 size-full opacity-[0.12]" xmlns="http://www.w3.org/2000/svg">
        <defs>
          <pattern id="nx-grid" width="32" height="32" patternUnits="userSpaceOnUse">
            <path d="M32 0H0V32" fill="none" stroke="white" strokeWidth="1" />
          </pattern>
          <radialGradient id="nx-fade" cx="30%" cy="30%" r="80%">
            <stop offset="0" stopColor="white" />
            <stop offset="1" stopColor="white" stopOpacity="0" />
          </radialGradient>
          <mask id="nx-mask">
            <rect width="100%" height="100%" fill="url(#nx-fade)" />
          </mask>
        </defs>
        <rect width="100%" height="100%" fill="url(#nx-grid)" mask="url(#nx-mask)" />
      </svg>
      <div className="absolute -right-24 top-1/3 size-80 rounded-full bg-brand-accent/25 blur-3xl" />
    </div>
  )
}
