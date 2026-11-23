import { Check, ChevronRight, ChevronsUpDown, LogOut, WifiOff } from 'lucide-react'
import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { NavLink, Outlet, useLocation } from 'react-router'
import { Logo, LogoMark } from '@/components/brand/Logo'
import { Avatar } from '@/components/ui/avatar'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuGroup,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { cn } from '@/lib/utils'
import { chooseOrganization, type Session, signOut, useSession } from '@/session/session'
import { allows, type Section, sectionGroups, sections } from './sections'
import { ThemeRadioItems } from './ThemeMenu'

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

type ActiveSession = Extract<Session, { status: 'active' }>

/**
 * Marco de la aplicación (design de refine-admin-panel-design, decisión 4): barra lateral fija con logo, organización
 * activa y secciones agrupadas en pantallas grandes; encabezado con la ubicación y el menú de la cuenta; menú inferior
 * en celular. Las secciones salen de la tabla de sections.ts según los permisos.
 */
export function Layout() {
  const { t } = useTranslation()
  const session = useSession()
  const online = useOnline()
  const location = useLocation()
  if (session.status !== 'active') {
    return null
  }

  const visible = sections.filter((section) => allows(session, section.requirement))
  const organization = session.user.organizations.find((o) => o.id === session.organizationId)
  const current = sections.find((section) => section.path !== '/' && location.pathname.startsWith(section.path))

  return (
    <div className="min-h-dvh md:grid md:grid-cols-[16rem_minmax(0,1fr)]">
      <a
        href="#content"
        className="sr-only z-50 rounded-md bg-primary px-3 py-2 text-sm text-primary-foreground focus:not-sr-only focus:fixed focus:top-3 focus:left-3"
      >
        {t('app.skipToContent')}
      </a>

      <aside className="sticky top-0 hidden h-dvh flex-col border-r border-sidebar-border bg-sidebar text-sidebar-foreground md:flex">
        <div className="flex h-16 shrink-0 items-center px-5">
          <Logo />
        </div>

        {organization && (
          <div className="mx-3 mb-2 flex items-center gap-3 rounded-lg border border-sidebar-border bg-card px-3 py-2.5 shadow-xs">
            <Avatar text={organization.name} className="rounded-md" />
            <div className="min-w-0">
              <p className="text-[11px] font-medium tracking-wide text-muted-foreground uppercase">{t('organization.current')}</p>
              <p className="truncate text-sm font-medium text-foreground">{organization.name}</p>
            </div>
          </div>
        )}

        <nav aria-label={t('sections.navigation')} className="flex-1 overflow-y-auto px-3 py-2">
          {sectionGroups.map((group) => {
            const items = visible.filter((section) => section.group === group.id)
            if (items.length === 0) {
              return null
            }

            return (
              <div key={group.id} className="mb-4">
                {group.labelKey && (
                  <p className="px-3 pb-1.5 text-[11px] font-semibold tracking-wider text-muted-foreground uppercase">{t(group.labelKey)}</p>
                )}
                <ul className="space-y-0.5">
                  {items.map((section) => (
                    <li key={section.id}>
                      <SidebarLink section={section} />
                    </li>
                  ))}
                </ul>
              </div>
            )
          })}
        </nav>

        <div className="border-t border-sidebar-border p-3">
          <div className="flex items-center gap-3 px-2 py-1.5">
            <Avatar text={session.user.email} />
            <p className="min-w-0 truncate text-sm">{session.user.email}</p>
          </div>
        </div>
      </aside>

      <div className="flex min-h-dvh min-w-0 flex-col">
        <header className="sticky top-0 z-30 flex h-16 shrink-0 items-center gap-3 border-b bg-background/85 px-4 backdrop-blur-md md:px-8">
          <LogoMark className="md:hidden" />
          <p className="flex min-w-0 items-center gap-1.5 text-sm text-muted-foreground">
            <span className="hidden truncate sm:inline">{organization?.name}</span>
            {current && (
              <>
                <ChevronRight className="hidden size-4 shrink-0 sm:inline" aria-hidden="true" />
                <span className="truncate font-medium text-foreground">{t(current.labelKey)}</span>
              </>
            )}
          </p>
          <div className="ml-auto">
            <UserMenu session={session} />
          </div>
        </header>

        {!online && (
          <div role="status" className="flex animate-fade-in items-center justify-center gap-2 border-b border-warning/30 bg-warning/10 px-4 py-2 text-sm text-warning">
            <WifiOff className="size-4" aria-hidden="true" />
            {t('offline.banner')}
          </div>
        )}

        <main id="content" tabIndex={-1} className="mx-auto w-full max-w-6xl flex-1 px-4 py-6 pb-28 outline-none md:px-8 md:py-8 md:pb-10">
          <div key={location.pathname} className="animate-rise">
            <Outlet />
          </div>
        </main>
      </div>

      <nav
        aria-label={t('sections.navigation')}
        className="fixed inset-x-0 bottom-0 z-40 flex overflow-x-auto border-t bg-card/95 pb-[env(safe-area-inset-bottom)] backdrop-blur-md md:hidden"
      >
        {visible.map((section) => (
          <NavLink
            key={section.id}
            to={section.path}
            end={section.path === '/'}
            className={({ isActive }) =>
              cn(
                'relative flex min-w-18 flex-1 flex-col items-center gap-1 px-2 pt-2.5 pb-2 text-[11px] font-medium transition-colors outline-none focus-visible:bg-accent',
                isActive ? 'text-primary' : 'text-muted-foreground',
              )
            }
          >
            {({ isActive }) => (
              <>
                {isActive && <span className="absolute top-0 h-0.5 w-8 rounded-full bg-primary" aria-hidden="true" />}
                <section.icon className="size-5" aria-hidden="true" />
                <span className="max-w-full truncate">{t(section.labelKey)}</span>
              </>
            )}
          </NavLink>
        ))}
      </nav>
    </div>
  )
}

function SidebarLink({ section }: { section: Section }) {
  const { t } = useTranslation()
  return (
    <NavLink
      to={section.path}
      end={section.path === '/'}
      className={({ isActive }) =>
        cn(
          'group relative flex items-center gap-3 rounded-md px-3 py-2 text-sm transition-colors outline-none',
          'hover:bg-sidebar-accent/60 hover:text-sidebar-accent-foreground focus-visible:ring-2 focus-visible:ring-ring',
          isActive && 'bg-sidebar-accent font-medium text-sidebar-accent-foreground',
        )
      }
    >
      {({ isActive }) => (
        <>
          {isActive && <span className="absolute top-1.5 bottom-1.5 -left-3 w-1 rounded-r-full bg-primary" aria-hidden="true" />}
          <section.icon className={cn('size-4 shrink-0', isActive ? 'text-primary' : 'text-muted-foreground group-hover:text-current')} aria-hidden="true" />
          {t(section.labelKey)}
        </>
      )}
    </NavLink>
  )
}

function UserMenu({ session }: { session: ActiveSession }) {
  const { t } = useTranslation()
  return (
    <DropdownMenu>
      <DropdownMenuTrigger
        aria-label={t('user.menu')}
        className="flex items-center gap-2 rounded-full p-1 pr-2 text-sm transition-colors outline-none hover:bg-accent focus-visible:ring-2 focus-visible:ring-ring data-[state=open]:bg-accent"
      >
        <Avatar text={session.user.email} />
        <ChevronsUpDown className="size-3.5 text-muted-foreground" aria-hidden="true" />
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="w-64">
        <div className="flex items-center gap-3 px-2 py-2">
          <Avatar text={session.user.email} className="size-9" />
          <div className="min-w-0">
            <p className="truncate text-sm font-medium">{session.user.email}</p>
            {session.user.platformOperator && <p className="text-xs text-muted-foreground">{t('user.platformOperator')}</p>}
          </div>
        </div>
        {session.user.organizations.length > 1 && (
          <>
            <DropdownMenuSeparator />
            <DropdownMenuGroup>
              <DropdownMenuLabel>{t('organization.switch')}</DropdownMenuLabel>
              {session.user.organizations.map((o) => (
                <DropdownMenuItem key={o.id} disabled={o.id === session.organizationId} onSelect={() => void chooseOrganization(o.id)}>
                  <Avatar text={o.name} className="size-5 rounded text-[9px]" />
                  <span className="flex-1 truncate">{o.name}</span>
                  {o.id === session.organizationId && <Check className="text-primary" />}
                </DropdownMenuItem>
              ))}
            </DropdownMenuGroup>
          </>
        )}
        <DropdownMenuSeparator />
        <ThemeRadioItems />
        <DropdownMenuSeparator />
        <DropdownMenuItem onSelect={() => void signOut()}>
          <LogOut />
          {t('user.signOut')}
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  )
}
