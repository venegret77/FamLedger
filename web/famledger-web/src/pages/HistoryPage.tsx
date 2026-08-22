import { useEffect, useMemo, useState } from 'react'
import { usePeriodHistory, usePeriods, useTransactions } from '../api/hooks'
import { Card, CardTitle } from '../components/ui/Card'
import { Button } from '../components/ui/Button'
import { EmptyState, PageHeader, Spinner, Tabs, Badge } from '../components/ui/Tabs'
import { MoneyDisplay } from '../components/ui/MoneyDisplay'
import { formatDate, formatMoney } from '../lib/format'
import { Select } from '../components/ui/Input'

const viewTabs = [
  { id: 'overview', label: 'Сводка' },
  { id: 'category', label: 'Категории' },
  { id: 'day', label: 'По дням' },
  { id: 'list', label: 'Операции' },
]

export function HistoryPage() {
  const { data: periods, isLoading, isError, refetch } = usePeriods()
  const [selectedId, setSelectedId] = useState<string>('')
  const [activeTab, setActiveTab] = useState('overview')

  useEffect(() => {
    if (!periods?.length) return
    if (!selectedId || !periods.some((p) => p.id === selectedId)) {
      const preferred = periods.find((p) => p.isClosed) ?? periods[0]
      setSelectedId(preferred.id)
    }
  }, [periods, selectedId])

  const { data: detail, isLoading: detailLoading } = usePeriodHistory(selectedId || undefined)
  const { data: transactions, isLoading: txLoading } = useTransactions(
    selectedId || undefined,
  )

  const periodOptions = useMemo(
    () =>
      (periods ?? []).map((p) => ({
        value: p.id,
        label: p.isActive ? `${p.label} · текущий` : p.label,
      })),
    [periods],
  )

  const currency = detail?.currency ?? periods?.[0]?.currency ?? 'RSD'

  function titleFor(tx: { note?: string; categoryName?: string; kind?: string }) {
    if (tx.note) return tx.note
    if (tx.categoryName) return tx.categoryName
    return (tx.kind ?? 'Expense') === 'Income' ? 'Пополнение' : 'Расход'
  }

  if (isLoading) {
    return (
      <div className="flex justify-center py-20">
        <Spinner />
      </div>
    )
  }

  if (isError) {
    return (
      <EmptyState
        title="Не удалось загрузить историю"
        action={
          <Button variant="secondary" onClick={() => void refetch()}>
            Повторить
          </Button>
        }
      />
    )
  }

  if (!periods?.length) {
    return (
      <div className="space-y-6">
        <PageHeader title="История" subtitle="Закрытые месяцы и статистика" />
        <EmptyState
          title="Пока нет периодов"
          description="История появится после первого месяца."
        />
      </div>
    )
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title="История"
        subtitle="Просмотр месяцев: сводка, категории, дни и операции"
      />

      <Select
        label="Период"
        value={selectedId}
        onChange={(e) => {
          setSelectedId(e.target.value)
          setActiveTab('overview')
        }}
        options={periodOptions}
      />

      <Tabs tabs={viewTabs} activeTab={activeTab} onChange={setActiveTab} />

      {detailLoading || !detail ? (
        <div className="flex justify-center py-12">
          <Spinner />
        </div>
      ) : activeTab === 'overview' ? (
        <div className="space-y-4">
          <Card>
            <div className="flex flex-wrap items-center gap-2">
              <CardTitle>{detail.label}</CardTitle>
              {detail.isActive ? (
                <Badge variant="success">Текущий</Badge>
              ) : (
                <Badge>Закрыт</Badge>
              )}
            </div>
            <p className="mt-1 text-sm text-slate-500">
              {formatDate(detail.startDate)} — {formatDate(detail.endDate)}
              {detail.closedAt ? ` · закрыт ${formatDate(detail.closedAt)}` : null}
            </p>
            <dl className="mt-4 grid gap-3 sm:grid-cols-2">
              <StatRow label="Доходы" value={formatMoney(detail.income, currency)} />
              <StatRow label="Пополнения" value={formatMoney(detail.topUps, currency)} />
              <StatRow label="План" value={formatMoney(detail.plannedExpenses, currency)} />
              <StatRow label="Факт расходов" value={formatMoney(detail.spent, currency)} />
              <StatRow label="Остаток" value={formatMoney(detail.remaining, currency)} />
              <StatRow
                label="Операции"
                value={`${detail.transactionCount} (${detail.expenseCount} списаний)`}
              />
            </dl>
          </Card>
        </div>
      ) : activeTab === 'category' ? (
        !detail.byCategory.length ? (
          <EmptyState title="Нет расходов по категориям" />
        ) : (
          <Card padding="none">
            <ul className="divide-y divide-slate-100">
              {detail.byCategory.map((item) => (
                <li
                  key={item.name}
                  className="flex items-center justify-between gap-4 px-5 py-3 text-sm"
                >
                  <div>
                    <p className="font-medium text-slate-900">{item.name}</p>
                    <p className="text-slate-500">{item.count} оп.</p>
                  </div>
                  <span className="font-medium text-slate-900">
                    {formatMoney(item.amount, currency)}
                  </span>
                </li>
              ))}
            </ul>
          </Card>
        )
      ) : activeTab === 'day' ? (
        !detail.byDay.length ? (
          <EmptyState title="Нет данных по дням" />
        ) : (
          <Card padding="none">
            <ul className="divide-y divide-slate-100">
              {detail.byDay.map((day) => (
                <li key={day.date} className="px-5 py-3 text-sm">
                  <p className="font-medium text-slate-900">{formatDate(day.date)}</p>
                  <div className="mt-1 flex flex-wrap gap-4 text-slate-600">
                    <span>Расход: {formatMoney(day.spent, currency)}</span>
                    {day.topUps > 0 && (
                      <span>Пополнения: {formatMoney(day.topUps, currency)}</span>
                    )}
                  </div>
                </li>
              ))}
            </ul>
          </Card>
        )
      ) : txLoading ? (
        <div className="flex justify-center py-12">
          <Spinner />
        </div>
      ) : !transactions?.length ? (
        <EmptyState title="Операций в этом периоде нет" />
      ) : (
        <Card padding="none">
          <ul className="divide-y divide-slate-100">
            {transactions.map((tx) => (
              <li key={tx.id} className="flex items-start justify-between gap-4 px-5 py-4">
                <div className="min-w-0">
                  <div className="flex flex-wrap items-center gap-2">
                    <p className="truncate font-medium text-slate-900">{titleFor(tx)}</p>
                    {(tx.kind ?? 'Expense') === 'Income' && (
                      <Badge variant="success">Пополнение</Badge>
                    )}
                  </div>
                  <p className="mt-0.5 text-sm text-slate-500">
                    {formatDate(tx.date)}
                    {tx.createdByName ? ` · ${tx.createdByName}` : null}
                  </p>
                </div>
                <MoneyDisplay
                  amount={(tx.kind ?? 'Expense') === 'Income' ? tx.amount : -tx.amount}
                  currency={tx.currency}
                  size="sm"
                  signed
                  className="shrink-0"
                />
              </li>
            ))}
          </ul>
        </Card>
      )}
    </div>
  )
}

function StatRow({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-xl bg-slate-50 px-4 py-3">
      <dt className="text-xs font-medium uppercase tracking-wide text-slate-500">{label}</dt>
      <dd className="mt-1 text-sm font-semibold text-slate-900">{value}</dd>
    </div>
  )
}
