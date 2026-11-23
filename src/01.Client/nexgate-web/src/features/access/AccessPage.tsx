import { CalendarClock, FlaskConical, Users } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { useSearchParams } from 'react-router'
import { SiteGate, SitePicker, useSelectedSite } from '@/components/site-picker/SitePicker'
import { PageHeader } from '@/components/ui/page-header'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { SchedulesTab } from './SchedulesTab'
import { SimulationForm } from './SimulationForm'
import { SubjectsTab } from './SubjectsTab'

/** Pestaña en la dirección (design de add-access-screens, decisión 1): Personas no lleva valor. */
const viewParam = 'vista'
const views = { subjects: null, schedules: 'horarios', test: 'probar' } as const
type View = keyof typeof views

export function AccessPage() {
  const { t } = useTranslation()
  // Los datos de acceso exigen el permiso sobre el sitio entero: un alcance de espacio no alcanza (spec subjects).
  const selected = useSelectedSite({ permission: 'access.read' })
  const [params, setParams] = useSearchParams()
  const raw = params.get(viewParam)
  const view: View = raw === views.schedules ? 'schedules' : raw === views.test ? 'test' : 'subjects'

  function setView(next: string) {
    setParams((current) => {
      const updated = new URLSearchParams(current)
      const value = views[next as View]
      if (value) updated.set(viewParam, value)
      else updated.delete(viewParam)
      return updated
    })
  }

  return (
    <div className="space-y-6">
      <PageHeader title={t('access.title')} description={t('access.description')} actions={<SitePicker selected={selected} />} />
      <SiteGate selected={selected} noSites={{ title: t('access.noSites'), description: t('access.noSitesDescription') }}>
        {(site) => (
          <Tabs key={site.id} value={view} onValueChange={setView} className="space-y-5">
            <TabsList aria-label={t('spaces.tabs.label')} className="max-sm:[&_svg]:hidden">
              <TabsTrigger value="subjects">
                <Users aria-hidden="true" />
                {t('access.tabs.subjects')}
              </TabsTrigger>
              <TabsTrigger value="schedules">
                <CalendarClock aria-hidden="true" />
                {t('access.tabs.schedules')}
              </TabsTrigger>
              <TabsTrigger value="test">
                <FlaskConical aria-hidden="true" />
                {t('access.tabs.test')}
              </TabsTrigger>
            </TabsList>
            <TabsContent value="subjects">
              <SubjectsTab site={site} />
            </TabsContent>
            <TabsContent value="schedules">
              <SchedulesTab site={site} />
            </TabsContent>
            <TabsContent value="test">
              <div className="max-w-2xl rounded-xl border bg-card p-6 shadow-xs">
                <SimulationForm site={site} />
              </div>
            </TabsContent>
          </Tabs>
        )}
      </SiteGate>
    </div>
  )
}
