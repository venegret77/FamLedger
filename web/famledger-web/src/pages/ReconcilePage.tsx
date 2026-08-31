import { useEffect, useMemo, useState, type FormEvent } from 'react'
import { useReconciliation, useSaveReconciliation, useSettings } from '../api/hooks'
import {
  currencyOptions,
  type ReconciliationAmount,
  type ReconciliationManualEntry,
  type ReconciliationManualInput,
} from '../api/types'
import { Card, CardTitle } from '../components/ui/Card'
import { Button } from '../components/ui/Button'
import { Input, Select } from '../components/ui/Input'
import { EmptyState, PageHeader, Spinner } from '../components/ui/Tabs'
import { useConfirmDialog } from '../components/ui/ConfirmDialog'
import { formatMoney } from '../lib/format'

type ManualSide = 'assetItems' | 'obligationItems'

function emptyManual(): ReconciliationManualInput {
  return { assetItems: [], obligationItems: [] }
}

function newEntryId(): string {
  return crypto.randomUUID()
}

function formatAmounts(amounts: { currency: string; amount: number }[]): string {
  if (amounts.length === 0) return '—'
  return amounts.map((a) => formatMoney(a.amount, a.currency)).join(' · ')
}

export function ReconcilePage() {
  const { confirm } = useConfirmDialog()
  const { data: settings } = useSettings()
  const { data, isLoading, isError, refetch } = useReconciliation()
  const save = useSaveReconciliation()
  const [manual, setManual] = useState<ReconciliationManualInput>(emptyManual)

  useEffect(() => {
    if (data?.manual) {
      setManual(data.manual)
    }
  }, [data?.manual])

  const baseCurrency = data?.baseCurrency ?? settings?.baseCurrency ?? 'RSD'

  const factVsLedgerTone = useMemo(() => {
    if (!data) return 'neutral' as const
    const gap = data.summary.actualTotal - data.summary.ledgerTotal
    if (Math.abs(gap) < 1) return 'neutral' as const
    return gap > 0 ? ('ok' as const) : ('bad' as const)
  }, [data])

  async function addItem(side: ManualSide, entry: ReconciliationManualEntry) {
    const next = {
      ...manual,
      [side]: [...manual[side], entry],
    }
    setManual(next)
    await save.mutateAsync(next)
  }

  async function removeItem(side: ManualSide, id: string) {
    const next = {
      ...manual,
      [side]: manual[side].filter((item) => item.id !== id),
    }
    setManual(next)
    await save.mutateAsync(next)
  }

  const assetTotals = useMemo(
    () => mergeTotals(data?.assets.lines.filter((l) => !l.isManual) ?? [], manual.assetItems),
    [data?.assets.lines, manual.assetItems],
  )
  const obligationTotals = useMemo(
    () =>
      mergeTotals(
        data?.obligations.lines.filter((l) => !l.isManual) ?? [],
        manual.obligationItems,
      ),
    [data?.obligations.lines, manual.obligationItems],
  )

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
            ? `${data.periodLabel} · добавляйте пункты и сравнивайте с учётом`
            : `${data.periodLabel} · просмотр (редактирование доступно главе и помощнику)`
        }
      />

      <div className="grid gap-4 xl:grid-cols-2">
        <ReconciliationSideCard
          title="Активы"
          subtitle="Что есть на руках"
          autoLines={data.assets.lines.filter((line) => !line.isManual)}
          manualItems={manual.assetItems}
          totals={assetTotals}
          totalBase={data.summary.assetTotal}
          baseCurrency={baseCurrency}
          canEdit={data.canEdit}
          addLabel="Добавить актив"
          namePlaceholder="Карты, наличные, копилка…"
          onAdd={(entry) => void addItem('assetItems', entry)}
          onRemove={async (id) => {
            const accepted = await confirm({
              title: 'Удалить пункт?',
              message: 'Строка будет убрана из сверки.',
            })
            if (accepted) await removeItem('assetItems', id)
          }}
          saving={save.isPending}
        />
        <ReconciliationSideCard
          title="Обязательства"
          subtitle="Что ещё нужно отдать"
          autoLines={data.obligations.lines.filter((line) => !line.isManual)}
          manualItems={manual.obligationItems}
          totals={obligationTotals}
          totalBase={data.summary.obligationTotal}
          baseCurrency={baseCurrency}
          canEdit={data.canEdit}
          addLabel="Добавить обязательство"
          namePlaceholder="Копилка, резерв…"
          onAdd={(entry) => void addItem('obligationItems', entry)}
          onRemove={async (id) => {
            const accepted = await confirm({
              title: 'Удалить пункт?',
              message: 'Строка будет убрана из сверки.',
            })
            if (accepted) await removeItem('obligationItems', id)
          }}
          saving={save.isPending}
        />
      </div>

      <Card className="border-emerald-200/80 bg-gradient-to-br from-emerald-50/80 to-white">
        <CardTitle>Итог</CardTitle>
        <div className="mt-4 grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-5">
          <SummaryItem
            label="Активы"
            amount={data.summary.assetTotal}
            currency={baseCurrency}
          />
          <SummaryItem
            label="Обязательства"
            amount={data.summary.obligationTotal}
            currency={baseCurrency}
          />
          <SummaryItem
            label="По учёту"
            amount={data.summary.ledgerTotal}
            currency={baseCurrency}
          />
          <SummaryItem
            label="По факту"
            amount={data.summary.actualTotal}
            currency={baseCurrency}
            tone={factVsLedgerTone}
          />
          <SummaryItem
            label="Разница"
            amount={data.summary.difference}
            currency={baseCurrency}
            tone={factVsLedgerTone}
          />
        </div>
      </Card>
    </div>
  )
}

function mergeTotals(
  autoLines: { amounts: ReconciliationAmount[] }[],
  manualItems: ReconciliationManualEntry[],
): ReconciliationAmount[] {
  const map = new Map<string, number>()
  for (const line of autoLines) {
    for (const amount of line.amounts) {
      map.set(amount.currency, (map.get(amount.currency) ?? 0) + amount.amount)
    }
  }
  for (const item of manualItems) {
    map.set(item.currency, (map.get(item.currency) ?? 0) + item.amount)
  }
  return [...map.entries()]
    .filter(([, amount]) => amount !== 0)
    .sort(([a], [b]) => a.localeCompare(b))
    .map(([currency, amount]) => ({ currency, amount }))
}

function ReconciliationSideCard({
  title,
  subtitle,
  autoLines,
  manualItems,
  totals,
  totalBase,
  baseCurrency,
  canEdit,
  addLabel,
  namePlaceholder,
  onAdd,
  onRemove,
  saving = false,
}: {
  title: string
  subtitle: string
  autoLines: { key: string; label: string; amounts: { currency: string; amount: number }[] }[]
  manualItems: ReconciliationManualEntry[]
  totals: { currency: string; amount: number }[]
  totalBase: number
  baseCurrency: string
  canEdit: boolean
  addLabel: string
  namePlaceholder: string
  onAdd: (entry: ReconciliationManualEntry) => void | Promise<void>
  onRemove: (id: string) => void | Promise<void>
  saving?: boolean
}) {
  return (
    <Card padding="none" className="overflow-hidden">
      <div className="border-b border-slate-100 bg-emerald-700 px-5 py-3 text-white">
        <p className="font-semibold">{title}</p>
        <p className="text-sm text-emerald-100">{subtitle}</p>
      </div>

      <div className="divide-y divide-slate-100">
        {autoLines.map((line) => (
          <div key={line.key} className="flex items-start justify-between gap-3 px-5 py-4">
            <div>
              <p className="font-medium text-slate-900">{line.label}</p>
              <p className="mt-0.5 text-xs text-slate-500">из приложения</p>
            </div>
            <p className="text-right text-sm font-medium tabular-nums text-slate-800">
              {formatAmounts(line.amounts)}
            </p>
          </div>
        ))}

        {manualItems.length > 0 && (
          <ul className="divide-y divide-slate-100">
            {manualItems.map((item) => (
              <li key={item.id} className="flex items-center justify-between gap-3 px-5 py-3">
                <div className="min-w-0">
                  <p className="font-medium text-slate-900">{item.name}</p>
                  <p className="mt-0.5 text-sm tabular-nums text-slate-600">
                    {formatMoney(item.amount, item.currency)}
                  </p>
                </div>
                {canEdit && (
                  <Button
                    variant="ghost"
                    size="sm"
                    className="shrink-0 text-red-600 hover:bg-red-50"
                    onClick={() => onRemove(item.id)}
                  >
                    Удалить
                  </Button>
                )}
              </li>
            ))}
          </ul>
        )}

        {canEdit && (
          <div className="px-5 py-4">
            <AddManualItemForm
              label={addLabel}
              namePlaceholder={namePlaceholder}
              defaultCurrency={baseCurrency}
              loading={saving}
              onAdd={onAdd}
            />
          </div>
        )}

        {!canEdit && manualItems.length === 0 && autoLines.length === 0 && (
          <p className="px-5 py-4 text-sm text-slate-500">Нет данных</p>
        )}
      </div>

      <div className="border-t border-slate-200 bg-slate-50 px-5 py-3">
        <div className="flex items-center justify-between gap-3">
          <p className="text-sm font-semibold text-slate-700">Итого</p>
          <div className="text-right">
            <p className="text-sm font-bold tabular-nums text-slate-900">{formatAmounts(totals)}</p>
            <p className="text-xs text-slate-500">≈ {formatMoney(totalBase, baseCurrency)}</p>
          </div>
        </div>
      </div>
    </Card>
  )
}

function AddManualItemForm({
  label,
  namePlaceholder,
  defaultCurrency,
  loading,
  onAdd,
}: {
  label: string
  namePlaceholder: string
  defaultCurrency: string
  loading?: boolean
  onAdd: (entry: ReconciliationManualEntry) => void | Promise<void>
}) {
  const [name, setName] = useState('')
  const [amount, setAmount] = useState('')
  const [currency, setCurrency] = useState(defaultCurrency)

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    const parsed = Number.parseFloat(amount.replace(',', '.'))
    const trimmed = name.trim()
    if (!trimmed || Number.isNaN(parsed) || parsed <= 0) return

    await onAdd({
      id: newEntryId(),
      name: trimmed,
      amount: parsed,
      currency,
    })
    setName('')
    setAmount('')
  }

  return (
    <form className="space-y-3" onSubmit={(e) => void handleSubmit(e)}>
      <p className="text-sm font-medium text-slate-700">{label}</p>
      <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4 lg:items-end">
        <Input
          label="Название"
          value={name}
          onChange={(e) => setName(e.target.value)}
          placeholder={namePlaceholder}
        />
        <Input
          label="Сумма"
          type="text"
          inputMode="decimal"
          value={amount}
          onChange={(e) => setAmount(e.target.value)}
          placeholder="0"
        />
        <Select
          label="Валюта"
          value={currency}
          onChange={(e) => setCurrency(e.target.value)}
          options={currencyOptions}
        />
        <Button type="submit" loading={loading} className="shrink-0">
          Добавить
        </Button>
      </div>
    </form>
  )
}

function SummaryItem({
  label,
  amount,
  currency,
  tone = 'neutral',
}: {
  label: string
  amount: number
  currency: string
  tone?: 'neutral' | 'ok' | 'bad'
}) {
  const boxClass =
    tone === 'ok'
      ? 'border-emerald-200 bg-emerald-50'
      : tone === 'bad'
        ? 'border-red-200 bg-red-50'
        : 'border-slate-200/80 bg-white'

  const amountClass =
    tone === 'ok'
      ? 'text-emerald-700'
      : tone === 'bad'
        ? 'text-red-700'
        : 'text-slate-900'

  return (
    <div className={`rounded-xl border px-4 py-3 ${boxClass}`}>
      <p className="text-xs font-medium uppercase tracking-wide text-slate-500">{label}</p>
      <p className={`mt-1 text-lg font-bold tabular-nums ${amountClass}`}>
        {formatMoney(amount, currency)}
      </p>
    </div>
  )
}
