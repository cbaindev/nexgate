import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { lazy, type ReactNode, Suspense, useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Route, Routes, useLocation } from 'react-router'
import { ApiError } from '@/api/errors'
import { SitesPage } from '@/features/sites/SitesPage'
import { SpacesPage } from '@/features/spaces/SpacesPage'
import { onSessionReset, restore, useSession } from '@/session/session'
import { ChooseOrganizationPage } from './ChooseOrganizationPage'
import { Layout } from './Layout'
import { ComingSoonPage, HomePage, LoadingPage, NoPermissionPage, NotFoundPage, OfflinePage } from './pages'
import { allows, type Section, sections } from './sections'
import { SignInPage } from './SignInPage'

export function createQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: {
        // Solo se reintenta sin conexión: un 401 ya lo reintenta el cliente tras renovar, y los demás errores se muestran
        // (un 500, con su código de referencia).
        retry: (count, error) => count < 2 && error instanceof ApiError && error.isNetwork,
        refetchOnWindowFocus: false,
      },
    },
  })
}

/** Muestra del sistema de diseño, solo en desarrollo: el build de producción no la incluye. */
const DesignPage = import.meta.env.DEV ? lazy(() => import('./DesignPage')) : null

const screens: Record<string, () => ReactNode> = {
  sites: () => <SitesPage />,
  spaces: () => <SpacesPage />,
}

/** Muestra la sección solo con su permiso; si no, "sin permiso" sin montar la pantalla (no pide datos). */
function RequireSection({ section }: { section: Section }) {
  const { t } = useTranslation()
  const session = useSession()
  if (!allows(session, section.requirement)) {
    return <NoPermissionPage />
  }

  const screen = section.built ? screens[section.id] : undefined
  return screen ? screen() : <ComingSoonPage title={t(section.labelKey)} />
}

export function AppRoutes() {
  const session = useSession()
  const location = useLocation()

  if (DesignPage && location.pathname === '/_design') {
    return (
      <Suspense fallback={<LoadingPage />}>
        <DesignPage />
      </Suspense>
    )
  }

  switch (session.status) {
    case 'restoring':
      return <LoadingPage />
    case 'offline':
      return <OfflinePage onRetry={() => void restore()} />
    case 'signedOut':
      return <SignInPage expired={session.expired} />
    case 'choosingOrganization':
      return <ChooseOrganizationPage user={session.user} unavailable={session.unavailable} />
    case 'active':
      return (
        <Routes>
          <Route element={<Layout />}>
            <Route index element={<HomePage />} />
            {sections
              .filter((section) => section.path !== '/')
              .map((section) => (
                <Route key={section.id} path={section.path} element={<RequireSection section={section} />} />
              ))}
            <Route path="*" element={<NotFoundPage />} />
          </Route>
        </Routes>
      )
  }
}

/** Raíz de la aplicación: caché de datos que se vacía en cada cambio de sesión u organización. */
export function App({ queryClient: provided, restoreOnStart = true }: { queryClient?: QueryClient; restoreOnStart?: boolean }) {
  const [queryClient] = useState(() => provided ?? createQueryClient())

  useEffect(() => onSessionReset(() => queryClient.clear()), [queryClient])
  useEffect(() => {
    if (restoreOnStart) {
      void restore()
    }
  }, [restoreOnStart])

  return (
    <QueryClientProvider client={queryClient}>
      <AppRoutes />
    </QueryClientProvider>
  )
}
