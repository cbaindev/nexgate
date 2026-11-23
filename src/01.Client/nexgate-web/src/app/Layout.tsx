import { Check, ChevronDown, LogOut, WifiOff } from 'lucide-react'
import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { NavLink, Outlet } from 'react-router'
import { Button } from '@/components/ui/button'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { cn } from '@/lib/utils'
import { chooseOrganization, signOut, useSession } from '@/session/session'
import { allows, sections } from './sections'

function useOnline() {
  const [online, setOnline] = useState(() => navigator.onLine)
  useEffect(() => {
    const update = () => setOnline(navigator.onLine)
    window.addEventListener('online', update)
    window.addEventListener('offline', update)
    return () => {
      window.removeEventListener('online', update)
      window.removeEventListener('offline', update)
    }
  }, [])
  return online
}

/** Marco de la aplicación: barra superior, navegación por permisos (lateral en pantallas grandes, inferior en celular). */
export function Layout() {
  const { t } = useTranslation()
  const session = useSession()
  const online = useOnline()
  if (session.status !== 'active') {
    return null
  }

  const visible = sections.filter((section) => allows(session, section.requirement))
  const organization = session.user.organizations.find((o) => o.id === session.organizationId)

  return (
    <div className="flex min-h-dvh flex-col">
      <header className="sticky top-0 z-40 flex h-14 items-center gap-3 border-b bg-card/95 px-4 backdrop-blur">
        <img src="/favicon.svg" alt="" className="size-7" />
        <span className="font-semibold">{t('app.name')}</span>
        <div className="ml-auto flex items-center gap-2">
          <DropdownMenu>
            <DropdownMenuTrigger asChild>
              <Button variant="ghost" size="sm" aria-label={t('user.menu')}>
                <span className="max-w-40 truncate">{organization?.name ?? session.user.email}</span>
                <ChevronDown />
              </Button>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end">
              <DropdownMenuLabel>{session.user.email}</DropdownMenuLabel>
              {session.user.platformOperator && <DropdownMenuLabel>{t('user.platformOperator')}</DropdownMenuLabel>}
              {session.user.organizations.length > 1 && (
                <>
                  <DropdownMenuSeparator />
                  <DropdownMenuLabel>{t('organization.switch')}</DropdownMenuLabel>
                  {session.user.organizations.map((o) => (
                    <DropdownMenuItem
                      key={o.id}
                      disabled={o.id === session.organizationId}
                      onSelect={() => void chooseOrganization(o.id)}
                    >
                      {o.id === session.organizationId ? <Check /> : <span className="size-4" />}
                      {o.name}
                    </DropdownMenuItem>
                  ))}
                </>
              )}
              <DropdownMenuSeparator />
              <DropdownMenuItem onSelect={() => void signOut()}>
                <LogOut />
                {t('user.signOut')}
              </DropdownMenuItem>
            </DropdownMenuContent>
          </DropdownMenu>
        </div>
      </header>

      {!online && (
        <div role="status" className="flex items-center justify-center gap-2 bg-muted px-4 py-1.5 text-sm text-muted-foreground">
          <WifiOff className="size-4" />
          {t('offline.banner')}
        </div>
      )}

      <div className="flex flex-1">
        <nav aria-label={t('sections.navigation')} className="hidden w-60 shrink-0 border-r bg-card p-3 md:block">
          <ul className="space-y-1">
            {visible.map((section) => (
              <li key={section.id}>
                <NavLink
                  to={section.path}
                  end={section.path === '/'}
                  className={({ isActive }) =>
                    cn(
                      'flex items-center gap-3 rounded-md px-3 py-2 text-sm hover:bg-accent',
                      isActive && 'bg-accent font-medium text-accent-foreground',
                    )
                  }
                >
                  <section.icon className="size-4" />
                  {t(section.labelKey)}
                </NavLink>
              </li>
            ))}
          </ul>
        </nav>

        <main id="content" className="min-w-0 flex-1 p-4 pb-24 md:p-8">
          <Outlet />
        </main>
      </div>

      <nav
        aria-label={t('sections.navigation')}
        className="fixed inset-x-0 bottom-0 z-40 flex overflow-x-auto border-t bg-card pb-[env(safe-area-inset-bottom)] md:hidden"
      >
        {visible.map((section) => (
          <NavLink
            key={section.id}
            to={section.path}
            end={section.path === '/'}
            className={({ isActive }) =>
              cn('flex min-w-20 flex-1 flex-col items-center gap-1 px-2 py-2 text-xs', isActive ? 'text-primary' : 'text-muted-foreground')
            }
          >
            <section.icon className="size-5" />
            <span className="truncate">{t(section.labelKey)}</span>
          </NavLink>
        ))}
      </nav>
    </div>
  )
}
