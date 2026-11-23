import { Monitor, Moon, Sun } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { Button } from '@/components/ui/button'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuLabel,
  DropdownMenuRadioGroup,
  DropdownMenuRadioItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { setThemePreference, type ThemePreference, themePreferences, useTheme } from '@/theme/theme'

const icons = { light: Sun, dark: Moon, system: Monitor }

/** Opciones de tema (claro, oscuro o sistema) para un menú desplegable. */
export function ThemeRadioItems() {
  const { t } = useTranslation()
  const { preference } = useTheme()
  return (
    <>
      <DropdownMenuLabel>{t('theme.label')}</DropdownMenuLabel>
      <DropdownMenuRadioGroup value={preference} onValueChange={(value) => setThemePreference(value as ThemePreference)}>
        {themePreferences.map((value) => {
          const Icon = icons[value]
          return (
            <DropdownMenuRadioItem key={value} value={value}>
              <Icon />
              {t(`theme.${value}`)}
            </DropdownMenuRadioItem>
          )
        })}
      </DropdownMenuRadioGroup>
    </>
  )
}

/** Botón de ícono con el selector de tema, para las pantallas sin sesión. */
export function ThemeButton({ className }: { className?: string }) {
  const { t } = useTranslation()
  const { resolved } = useTheme()
  const Icon = resolved === 'dark' ? Moon : Sun
  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button variant="ghost" size="icon" aria-label={t('theme.label')} className={className}>
          <Icon />
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="min-w-44">
        <ThemeRadioItems />
      </DropdownMenuContent>
    </DropdownMenu>
  )
}
