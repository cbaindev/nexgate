import { Building2, Plus } from 'lucide-react'
import { type ReactNode, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Logo } from '@/components/brand/Logo'
import { Alert } from '@/components/ui/alert'
import { Avatar } from '@/components/ui/avatar'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { ConfirmDialog } from '@/components/ui/confirm-dialog'
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from '@/components/ui/card'
import { EmptyState } from '@/components/ui/empty-state'
import { FieldError, FieldHint, Input, Label, Select } from '@/components/ui/form'
import { PageHeader } from '@/components/ui/page-header'
import { QrCode } from '@/components/ui/qr-code'
import { Separator } from '@/components/ui/separator'
import { Skeleton } from '@/components/ui/skeleton'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { ThemeButton } from './ThemeMenu'

// Nombres de los tokens de color (identificadores del sistema de diseño, no textos de interfaz).
const tokens = [
  'background',
  'card',
  'muted',
  'primary',
  'secondary',
  'accent',
  'destructive',
  'success',
  'warning',
  'info',
  'brand-accent',
  'border',
  'sidebar',
  'sidebar-accent',
]

function Section({ title, children }: { title: string; children: ReactNode }) {
  return (
    <section className="space-y-4">
      <h2 className="text-sm font-semibold tracking-wider text-muted-foreground uppercase">{title}</h2>
      {children}
    </section>
  )
}

/**
 * Muestra del sistema de diseño, solo en desarrollo (design de refine-admin-panel-design, decisión 5): la paleta y los
 * componentes, para revisarlos en claro y oscuro. El build de producción no la incluye.
 */
export default function DesignPage() {
  const { t } = useTranslation()
  const [confirming, setConfirming] = useState(false)
  return (
    <div className="mx-auto max-w-5xl space-y-12 px-4 py-8 md:px-8">
      <div className="flex items-center justify-between">
        <Logo />
        <ThemeButton />
      </div>
      <PageHeader title={t('design.title')} description={t('design.description')} />

      <Section title={t('design.palette')}>
        <ul className="grid grid-cols-2 gap-3 sm:grid-cols-4 lg:grid-cols-7">
          {tokens.map((token) => (
            <li key={token} className="overflow-hidden rounded-lg border bg-card text-xs">
              <div className="h-14 border-b" style={{ background: `var(--${token})` }} />
              <p className="px-2 py-1.5 font-mono">{token}</p>
            </li>
          ))}
        </ul>
      </Section>

      <Section title={t('design.buttons')}>
        <div className="flex flex-wrap items-center gap-3">
          <Button>
            <Plus />
            {t('sites.create')}
          </Button>
          <Button variant="secondary">{t('sites.edit')}</Button>
          <Button variant="outline">{t('sites.cancel')}</Button>
          <Button variant="ghost">{t('pages.backHome')}</Button>
          <Button variant="destructive">{t('user.signOut')}</Button>
          <Button variant="link">{t('pages.backHome')}</Button>
          <Button loading>{t('sites.saving')}</Button>
          <Button size="sm">{t('sites.save')}</Button>
          <Button size="lg">{t('signIn.submit')}</Button>
        </div>
      </Section>

      <Section title={t('design.badges')}>
        <div className="flex flex-wrap items-center gap-2">
          <Badge>{t('sites.types.building')}</Badge>
          <Badge variant="secondary">{t('sites.types.gated_community')}</Badge>
          <Badge variant="outline">{t('sites.types.company')}</Badge>
          <Badge variant="success">{t('design.states.success')}</Badge>
          <Badge variant="warning">{t('design.states.warning')}</Badge>
          <Badge variant="info">{t('design.states.info')}</Badge>
          <Badge variant="destructive">{t('design.states.danger')}</Badge>
          <Avatar text="ana.perez@nexgate.test" />
          <Avatar text="Torre Alvear" className="rounded-md" />
        </div>
      </Section>

      <Section title={t('design.alerts')}>
        <div className="grid gap-3 md:grid-cols-2">
          <Alert>{t('signIn.expired')}</Alert>
          <Alert variant="info">{t('update.available')}</Alert>
          <Alert variant="success">{t('design.states.success')}</Alert>
          <Alert variant="warning">{t('offline.banner')}</Alert>
          <Alert variant="destructive" className="md:col-span-2">
            {t('errors.unexpected')}
          </Alert>
        </div>
      </Section>

      <Section title={t('design.forms')}>
        <Card className="max-w-xl">
          <CardHeader>
            <CardTitle>{t('sites.createTitle')}</CardTitle>
            <CardDescription>{t('sites.formDescription')}</CardDescription>
          </CardHeader>
          <CardContent className="space-y-5">
            <div className="space-y-2">
              <Label htmlFor="design-name">{t('sites.name')}</Label>
              <Input id="design-name" placeholder={t('sites.namePlaceholder')} />
            </div>
            <div className="space-y-2">
              <Label htmlFor="design-invalid">{t('sites.name')}</Label>
              <Input id="design-invalid" aria-invalid defaultValue="" />
              <FieldError id="design-invalid-error" message={t('sites.nameRequired')} />
            </div>
            <div className="space-y-2">
              <Label htmlFor="design-type">{t('sites.type')}</Label>
              <Select id="design-type">
                <option>{t('sites.types.building')}</option>
              </Select>
              <FieldHint>{t('sites.typeHint')}</FieldHint>
            </div>
          </CardContent>
          <CardFooter className="justify-end">
            <Button variant="outline">{t('sites.cancel')}</Button>
            <Button>{t('sites.save')}</Button>
          </CardFooter>
        </Card>
      </Section>

      <Section title={t('design.loading')}>
        <div className="flex max-w-sm items-start gap-4 rounded-xl border bg-card p-5">
          <Skeleton className="size-11 rounded-lg" />
          <div className="flex-1 space-y-2.5 pt-1">
            <Skeleton className="h-4 w-2/3" />
            <Skeleton className="h-3 w-1/2" />
          </div>
        </div>
        <Separator />
        <div className="rounded-2xl border border-dashed bg-card/50">
          <EmptyState icon={Building2} title={t('sites.empty')} action={<Button variant="outline">{t('sites.createFirst')}</Button>}>
            {t('sites.emptyDescription')}
          </EmptyState>
        </div>
      </Section>

      <Section title={t('design.navigation')}>
        <Tabs defaultValue="spaces">
          <TabsList aria-label={t('spaces.tabs.label')}>
            <TabsTrigger value="spaces">{t('spaces.tabs.spaces')}</TabsTrigger>
            <TabsTrigger value="points">{t('spaces.tabs.points')}</TabsTrigger>
          </TabsList>
        </Tabs>
        <Table aria-label={t('spaces.tabs.points')}>
          <TableHeader>
            <TableRow>
              <TableHead>{t('spaces.points.name')}</TableHead>
              <TableHead>{t('spaces.points.type')}</TableHead>
              <TableHead>{t('spaces.points.withoutPower')}</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            <TableRow>
              <TableCell className="font-medium">Portón principal</TableCell>
              <TableCell>{t('spaces.points.types.gate')}</TableCell>
              <TableCell>
                <Badge variant="secondary">{t('spaces.points.failModesShort.fail_secure')}</Badge>
              </TableCell>
            </TableRow>
            <TableRow>
              <TableCell className="font-medium">Salida de emergencia</TableCell>
              <TableCell>{t('spaces.points.types.door')}</TableCell>
              <TableCell>
                <Badge variant="warning">{t('spaces.points.failModesShort.fail_safe')}</Badge>
              </TableCell>
            </TableRow>
          </TableBody>
        </Table>
        <div>
          <Button variant="destructive" onClick={() => setConfirming(true)}>
            {t('spaces.delete')}
          </Button>
        </div>
        <QrCode value="CLAIM-0001-XK7P" label={t('devices.claim.qrLabel')} className="size-32" />
        <ConfirmDialog
          open={confirming}
          onOpenChange={setConfirming}
          title={t('spaces.deleteTitle', { name: 'Piso 5' })}
          description={t('spaces.deleteDescription')}
          confirmLabel={t('spaces.delete')}
          cancelLabel={t('spaces.cancel')}
          onConfirm={() => setConfirming(false)}
        />
      </Section>
    </div>
  )
}
