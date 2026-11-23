import { ChevronRight, Search } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Badge } from '@/components/ui/badge'
import { Input } from '@/components/ui/form'
import { cn } from '@/lib/utils'
import { normalize, type Space, spaceIcons, type SpaceTree } from './model'

/**
 * Árbol de espacios con listas anidadas y un botón de despliegue por nodo (design de add-spaces-screens, decisión 3).
 * Empiezan desplegados la raíz, su primer nivel y los ancestros del espacio elegido; con filtro, lo necesario para ver
 * cada coincidencia.
 */
export function SpaceTreeView({ tree, selectedId, onSelect }: { tree: SpaceTree; selectedId: string | null; onSelect: (id: string) => void }) {
  const { t } = useTranslation()
  const [query, setQuery] = useState('')
  const ancestorsOf = (id: string | null) => (id ? (tree.byId.get(id)?.ancestorIds ?? []) : [])
  const [toggled, setToggled] = useState<Map<string, boolean>>(() => new Map(ancestorsOf(selectedId).map((id) => [id, true])))
  // Al elegir otro espacio se despliegan sus ancestros, y lo que ya estaba desplegado sigue así (no se pliega al subir).
  const [shown, setShown] = useState(selectedId)
  if (shown !== selectedId) {
    setShown(selectedId)
    setToggled((current) => {
      const next = new Map(current)
      ancestorsOf(selectedId).forEach((id) => next.set(id, true))
      return next
    })
  }

  const filter = tree.filter(query)

  function expanded(space: Space) {
    if (filter) {
      return tree.childrenOf(space.id).some((child) => filter.visible.has(child.id))
    }

    return toggled.get(space.id) ?? space.depth <= 1
  }

  function toggle(space: Space) {
    setToggled((current) => new Map(current).set(space.id, !expanded(space)))
  }

  const roots = filter ? tree.roots.filter((r) => filter.visible.has(r.id)) : tree.roots
  const wanted = normalize(query.trim())

  const renderNode = (space: Space) => {
    const children = tree.childrenOf(space.id).filter((child) => !filter || filter.visible.has(child.id))
    const isOpen = children.length > 0 && expanded(space)
    const Icon = spaceIcons[space.type] ?? spaceIcons.zone!
    const groupId = `space-children-${space.id}`
    const selected = space.id === selectedId
    return (
      <li key={space.id}>
        <div className="flex items-center gap-0.5" style={{ paddingLeft: `${space.depth * 1.25}rem` }}>
          {children.length > 0 ? (
            <button
              type="button"
              aria-expanded={isOpen}
              aria-controls={groupId}
              aria-label={t(isOpen ? 'spaces.collapse' : 'spaces.expand', { name: space.name })}
              onClick={() => toggle(space)}
              className="flex size-7 shrink-0 items-center justify-center rounded-md text-muted-foreground transition-colors outline-none hover:bg-accent hover:text-accent-foreground focus-visible:ring-2 focus-visible:ring-ring"
              disabled={!!filter}
            >
              <ChevronRight className={cn('size-4 transition-transform', isOpen && 'rotate-90')} aria-hidden="true" />
            </button>
          ) : (
            <span className="size-7 shrink-0" aria-hidden="true" />
          )}
          <button
            type="button"
            aria-current={selected ? 'true' : undefined}
            onClick={() => onSelect(space.id)}
            className={cn(
              'flex min-w-0 flex-1 items-center gap-2 rounded-md px-2 py-1.5 text-left text-sm transition-colors outline-none',
              'hover:bg-accent hover:text-accent-foreground focus-visible:ring-2 focus-visible:ring-ring',
              selected && 'bg-primary/10 font-medium text-primary hover:bg-primary/15 hover:text-primary',
            )}
          >
            <Icon className="size-4 shrink-0 opacity-80" role="img" aria-label={t(`spaces.types.${space.type}`, { defaultValue: space.type })} />
            <span className="truncate">
              <Highlight text={space.name} wanted={filter?.matches.has(space.id) ? wanted : ''} />
            </span>
            {space.isCommon && (
              <Badge variant="info" className="ml-auto shrink-0">
                {t('spaces.common')}
              </Badge>
            )}
          </button>
        </div>
        {isOpen && (
          <ul id={groupId} role="group">
            {children.map(renderNode)}
          </ul>
        )}
      </li>
    )
  }

  return (
    <div className="space-y-3">
      <div className="relative">
        <Search className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-muted-foreground" aria-hidden="true" />
        <Input
          type="search"
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          placeholder={t('spaces.filterPlaceholder')}
          aria-label={t('spaces.filter')}
          className="pl-9"
        />
      </div>
      {roots.length === 0 ? (
        <p className="px-2 py-6 text-center text-sm text-muted-foreground">{t('spaces.noMatches')}</p>
      ) : (
        <ul aria-label={t('spaces.treeLabel')} className="space-y-0.5">
          {roots.map(renderNode)}
        </ul>
      )}
    </div>
  )
}

/** Resalta el texto que coincide con el filtro (las posiciones de `normalize` coinciden con las del original). */
function Highlight({ text, wanted }: { text: string; wanted: string }) {
  const at = wanted ? normalize(text).indexOf(wanted) : -1
  if (at < 0) {
    return text
  }

  const chars = Array.from(text)
  return (
    <>
      {chars.slice(0, at).join('')}
      <mark className="rounded-sm bg-warning/25 px-0.5 text-inherit">{chars.slice(at, at + wanted.length).join('')}</mark>
      {chars.slice(at + wanted.length).join('')}
    </>
  )
}
