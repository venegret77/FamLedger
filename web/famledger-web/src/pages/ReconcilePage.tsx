import { useEffect, useMemo, useState } from 'react'
import { useReconciliation, useSaveReconciliation } from '../api/hooks'
import { CURRENCIES, type ReconciliationManualInput, type ReconciliationSide } from '../api/types'
import { Card, CardTitle } from '../components/ui/Card'
import { Button } from '../components/ui/Button'
import { Input } from '../components/ui/Input'
import { EmptyState, PageHeader, Spinner } from '../components/ui/Tabs'
import { formatMoney } from '../lib/format'

type ManualField = keyof ReconciliationManualInput

const manualFieldByLineKey: Record<string, ManualField> = {
  cards: 'cards',
  cash: 'cash',
  setAside: 'setAside',
  manualPlanned: 'manualPlanned',
  savingsPlan: 'savingsPlan',
}

function emptyManual(): ReconciliationManualInput {
  return { cards: {}, cash: {}, setAside: {}, manualPlanned: {}, savingsPlan: {} }
}

function formatAmounts(amounts: { currency: string; amount: number }[]): string {
  if (amounts.length === 0) return '—'
  return amounts.map((a) => formatMoney(a.amount, a.currency)).join(' · ')
}

export function ReconcilePage() {
  const { data, isLoading, isError, refetch } = useReconciliation()
  const save = useSaveReconciliation()
  const [manual, setManual] = useState<ReconciliationManualInput>(emptyManual)
  const [dirty, setDirty] = useState(false)

  useEffect(() => {
    if (data?.manual) {
      setManual(data.manual)
      setDirty(false)
    }
  }, [data?.manual])

  const baseCurrency = data?.baseCurrency ?? 'RSD'

  const differenceTone = useMemo(() => {
    if (!data) return 'neutral'
    const diff = Math.abs(data.summary.difference)
    if (diff < 1) return 'ok'
    if (diff < 1000) return 'warn'
    return 'bad'
  }, [data])

  function updateManualField(field: ManualField, currency: string, raw: string) {
    const parsed = raw.trim() === '' ? 0 : Number.parseFloat(raw.replace(',', '.'))
    if (raw.trim() !== '' && (Number.isNaN(parsed) || parsed < 0)) return

    setManual((prev) => {
      const next = { ...prev, [field]: { ...prev[field] } }
      if (parsed === 0) {
        delete next[field][currency]
      } else {
        next[field][currency] = parsed
      }
      return next
    })
    setDirty(true)
  }

  async function handleSave() {
    await save.mutateAsync(manual)
    setDirty(false)
  }

  if (isLoading) {
    return (
      <div className="flex justify-center py-20">
        <Spinner />
      </div>
    )
  }

  if (isError || !data) {
    return (
      <EmptyState
        title="Не удалось загрузить сверку"
        description="Проверьте подключение к API и попробуйте снова."
        action={
          <Button variant="secondary" onClick={() => void refetch()}>
            Повторить
          </Button>
        }
      />
    )
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title="Сверка"
        subtitle={
          data.canEdit
            ? `${data.periodLabel} · введите реальные остатки и сравните с учётом`
            : `${data.periodLabel} · просмотр (редактирование доступно главе и помощнику)`
        }
      />

      <div className="grid gap-4 xl:grid-cols-2">
        <ReconciliationSideCard
          title="Активы"
          subtitle="Что есть на руках"
          side={data.assets}
          baseCurrency={baseCurrency}
          manual={manual}
          canEdit={data.canEdit}
          onChange={updateManualField}
        />
        <ReconciliationSideCard
          title="Обязательства"
          subtitle="Что ещё нужно отдать"
          side={data.obligations}
          baseCurrency={baseCurrency}
          manual={manual}
          canEdit={data.canEdit}
          onChange={updateManualField}
        />
      </div>

      <Card className="border-emerald-200/80 bg-gradient-to-br from-emerald-50/80 to-white">
        <CardTitle>Итог</CardTitle>
        <div className="mt-4 grid gap-3 sm:grid-cols-2 lg:grid-cols-5">
          <SummaryItem
            label="Доходы (учёт)"
            amount={data.summary.ledgerIncome}
            currency={baseCurrency}
          />
          <SummaryItem
            label="Расходы (учёт)"
            amount={-data.summary.ledgerExpenses}
            currency={baseCurrency}
          />
          <SummaryItem
            label="По учёту"
            amount={data.summary.ledgerTotal}
            currency={baseCurrency}
            accent
          />
          <SummaryItem
            label="По факту"
            amount={data.summary.actualTotal}
            currency={baseCurrency}
            accent
          />
          <SummaryItem
            label="Разница"
            amount={data.summary.difference}
            currency={baseCurrency}
            accent
            tone={differenceTone}
            hint={
              Math.abs(data.summary.difference) < 1
                ? 'Сходится'
                : data.summary.difference > 0
                  ? 'В учёте больше, чем по факту'
                  : 'По факту больше, чем в учёте'
            }
          />
        </div>
        <p className="mt-4 text-sm text-slate-600">
          По факту = активы − обязательства (в {baseCurrency} по текущему курсу). Разница = по учёту − по
          факту.
        </p>
      </Card>

      {data.canEdit && (
        <div className="flex flex-wrap items-center gap-3">
          <Button onClick={() => void handleSave()} loading={save.isPending} disabled={!dirty}>
            Сохранить
          </Button>
          {dirty && <span className="text-sm text-amber-700">Есть несохранённые изменения</span>}
        </div>
      )}
    </div>
  )
}

function ReconciliationSideCard({
  title,
  subtitle,
  side,
  baseCurrency,
  manual,
  canEdit,
  onChange,
}: {
  title: string
  subtitle: string
  side: ReconciliationSide
  baseCurrency: string
  manual: ReconciliationManualInput
  canEdit: boolean
  onChange: (field: ManualField, currency: string, raw: string) => void
}) {
  return (
    <Card padding="none" className="overflow-hidden">
      <div className="border-b border-slate-100 bg-emerald-700 px-5 py-3 text-white">
        <p className="font-semibold">{title}</p>
        <p className="text-sm text-emerald-100">{subtitle}</p>
      </div>
      <div className="divide-y divide-slate-100">
        {side.lines.map((line) => {
          const field = manualFieldByLineKey[line.key]
          return (
            <div key={line.key} className="px-5 py-4">
              <div className="flex items-start justify-between gap-3">
                <div>
                  <p className="font-medium text-slate-900">{line.label}</p>
                  {!line.isManual && (
                    <p className="mt-0.5 text-xs text-slate-500">из приложения</p>
                  )}
                </div>
                {!line.isManual && (
                  <p className="text-right text-sm font-medium tabular-nums text-slate-800">
                    {formatAmounts(line.amounts)}
                  </p>
                )}
              </div>
              {line.isManual && canEdit && field && (
                <div className="mt-3 grid gap-2 sm:grid-cols-3">
                  {CURRENCIES.map((currency) => (
                    <Input
                      key={currency}
                      label={currency}
                      type="text"
                      inputMode="decimal"
                      placeholder="0"
                      value={
                        manual[field][currency] !== undefined
                          ? String(manual[field][currency])
                          : ''
                      }
                      onChange={(e) => onChange(field, currency, e.target.value)}
                    />
                  ))}
                </div>
              )}
              {line.isManual && !canEdit && (
                <p className="mt-2 text-sm tabular-nums text-slate-700">
                  {formatAmounts(line.amounts)}
                </p>
              )}
            </div>
          )
        })}
      </div>
      <div className="border-t border-slate-200 bg-slate-50 px-5 py-3">
        <div className="flex items-center justify-between gap-3">
          <p className="text-sm font-semibold text-slate-700">Итого</p>
          <div className="text-right">
            <p className="text-sm font-bold tabular-nums text-slate-900">
              {formatAmounts(side.totals)}
            </p>
            <p className="text-xs text-slate-500">
              ≈ {formatMoney(side.totalBase, baseCurrency)}
            </p>
          </div>
        </div>
      </div>
    </Card>
  )
}

function SummaryItem({
  label,
  amount,
  currency,
  accent,
  tone = 'neutral',
  hint,
}: {
  label: string
  amount: number
  currency: string
  accent?: boolean
  tone?: 'neutral' | 'ok' | 'warn' | 'bad'
  hint?: string
}) {
  const toneClass =
    tone === 'ok'
      ? 'text-emerald-700'
      : tone === 'warn'
        ? 'text-amber-700'
        : tone === 'bad'
          ? 'text-red-700'
          : accent
            ? 'text-slate-900'
            : 'text-slate-700'

  return (
    <div className="rounded-xl border border-slate-200/80 bg-white px-4 py-3">
      <p className="text-xs font-medium uppercase tracking-wide text-slate-500">{label}</p>
      <p className={`mt-1 text-lg font-bold tabular-nums ${toneClass}`}>
        {formatMoney(amount, currency)}
      </p>
      {hint && <p className="mt-1 text-xs text-slate-500">{hint}</p>}
    </div>
  )
}
