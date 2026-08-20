import { useMemo, useState } from 'react'
import { useDeleteTransaction, useTransactions } from '../api/hooks'
import { useConfirmDialog } from '../components/ui/ConfirmDialog'
import { Card } from '../components/ui/Card'
import { EmptyState, PageHeader, Spinner, Tabs } from '../components/ui/Tabs'
import { MoneyDisplay } from '../components/ui/MoneyDisplay'
import { Button } from '../components/ui/Button'
import { formatDate, formatMoney } from '../lib/format'

const viewTabs = [
  { id: 'list', label: 'Список' },
  { id: 'day', label: 'По дням' },
  { id: 'category', label: 'По категориям' },
]

export function TransactionsPage() {
  const { data: transactions, isLoading, isError, refetch } = useTransactions()
  const deleteTx = useDeleteTransaction()
  const { confirm } = useConfirmDialog()
  const [activeTab, setActiveTab] = useState('list')

  const byDay = useMemo(() => {
    if (!transactions?.length) return []
    const map = new Map<string, typeof transactions>()
    for (const tx of transactions) {
      const list = map.get(tx.date) ?? []
      list.push(tx)
      map.set(tx.date, list)
    }
    return [...map.entries()].sort((a, b) => b[0].localeCompare(a[0]))
  }, [transactions])

  const byCategory = useMemo(() => {
    if (!transactions?.length) return []
    const map = new Map<string, number>()
    for (const tx of transactions) {
      const key = tx.categoryName ?? 'Без категории'
      map.set(key, (map.get(key) ?? 0) + tx.baseAmount)
    }
    return [...map.entries()].sort((a, b) => b[1] - a[1])
  }, [transactions])

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
        title="Не удалось загрузить операции"
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
      <PageHeader title="Операции" subtitle="История расходов текущего периода" />

      <Tabs tabs={viewTabs} activeTab={activeTab} onChange={setActiveTab} />

      {!transactions?.length ? (
        <EmptyState
          title="Операций пока нет"
          description="Добавьте расход на главной странице или через бота."
        />
      ) : activeTab === 'list' ? (
        <Card padding="none">
          <ul className="divide-y divide-slate-100">
            {transactions.map((tx) => (
              <li key={tx.id} className="flex items-start justify-between gap-4 px-5 py-4">
                <div className="min-w-0">
                  <p className="truncate font-medium text-slate-900">
                    {tx.note || tx.categoryName || 'Расход'}
                  </p>
                  <p className="mt-0.5 text-sm text-slate-500">
                    {formatDate(tx.date)}
                    {tx.createdByName ? ` · ${tx.createdByName}` : ''}
                  </p>
                </div>
                <div className="flex items-center gap-2">
                  <MoneyDisplay amount={-tx.baseAmount} currency={tx.currency} />
                  <Button
                    variant="ghost"
                    size="sm"
                    className="text-red-600"
                    loading={deleteTx.isPending}
                    onClick={async () => {
                      const accepted = await confirm({
                        title: 'Удалить операцию?',
                        message: 'Операция будет удалена без возможности восстановления.',
                      })
                      if (accepted) {
                        void deleteTx.mutateAsync(tx.id)
                      }
                    }}
                  >
                    Удалить
                  </Button>
                </div>
              </li>
            ))}
          </ul>
        </Card>
      ) : activeTab === 'day' ? (
        <div className="space-y-4">
          {byDay.map(([date, items]) => (
            <Card key={date} padding="none">
              <div className="border-b border-slate-100 px-5 py-3 font-medium text-slate-900">
                {formatDate(date)} · {formatMoney(items.reduce((s, t) => s + t.baseAmount, 0), items[0]?.currency ?? 'RSD')}
              </div>
              <ul className="divide-y divide-slate-100">
                {items.map((tx) => (
                  <li key={tx.id} className="flex justify-between gap-4 px-5 py-3 text-sm">
                    <span>{tx.note || tx.categoryName || 'Расход'}</span>
                    <MoneyDisplay amount={-tx.baseAmount} currency={tx.currency} />
                  </li>
                ))}
              </ul>
            </Card>
          ))}
        </div>
      ) : (
        <Card padding="none">
          <ul className="divide-y divide-slate-100">
            {byCategory.map(([name, total]) => (
              <li key={name} className="flex justify-between gap-4 px-5 py-4">
                <span className="font-medium text-slate-900">{name}</span>
                <span className="text-slate-900">{formatMoney(total, transactions[0]?.currency ?? 'RSD')}</span>
              </li>
            ))}
          </ul>
        </Card>
      )}
    </div>
  )
}
