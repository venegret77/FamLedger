import { useMemo, useState } from 'react'
import {
  useCategories,
  useCreateTransaction,
  useDashboard,
  useTransactions,
} from '../api/hooks'
import { Card, CardTitle } from '../components/ui/Card'
import { Button } from '../components/ui/Button'
import { Input, Select } from '../components/ui/Input'
import { EmptyState, PageHeader, Spinner } from '../components/ui/Tabs'
import { StatCard } from '../components/ui/MoneyDisplay'
import { MobileMoreMenu } from '../components/layout/Navigation'
import { formatMoney } from '../lib/format'
import type { FormEvent } from 'react'

export function DashboardPage() {
  const { data: summary, isLoading, isError, refetch } = useDashboard()
  const { data: categories } = useCategories()
  const { data: transactions } = useTransactions()
  const createTransaction = useCreateTransaction()

  const [amount, setAmount] = useState('')
  const [categoryId, setCategoryId] = useState('')
  const [note, setNote] = useState('')

  const currency = summary?.currency ?? 'RSD'

  const byCategory = useMemo(() => {
    if (!transactions?.length) return []
    const map = new Map<string, number>()
    for (const tx of transactions) {
      const key = tx.categoryName ?? 'Без категории'
      map.set(key, (map.get(key) ?? 0) + tx.baseAmount)
    }
    return [...map.entries()].sort((a, b) => b[1] - a[1])
  }, [transactions])

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    const parsed = Number.parseFloat(amount.replace(',', '.'))
    if (Number.isNaN(parsed) || parsed <= 0) return

    await createTransaction.mutateAsync({
      amount: parsed,
      currency,
      categoryId: categoryId || undefined,
      note: note.trim() || undefined,
    })

    setAmount('')
    setNote('')
  }

  if (isLoading) {
    return (
      <div className="flex justify-center py-20">
        <Spinner />
      </div>
    )
  }

  if (isError || !summary) {
    return (
      <EmptyState
        title="Не удалось загрузить сводку"
        description="Проверьте подключение к API и попробуйте снова."
        action={
          <Button variant="secondary" onClick={() => void refetch()}>
            Повторить
          </Button>
        }
      />
    )
  }

  const categoryOptions = [
    { value: '', label: 'Без категории' },
    ...(categories?.map((c) => ({
      value: c.id,
      label: c.emoji ? `${c.emoji} ${c.name}` : c.name,
    })) ?? []),
  ]

  return (
    <div className="space-y-6">
      <PageHeader
        title="Главная"
        subtitle={summary.periodLabel || 'Текущий период'}
      />

      <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
        <StatCard
          label="Остаток месяца"
          amount={summary.remaining}
          currency={currency}
          hint={`Доходы ${formatMoney(summary.income, currency)} − план ${formatMoney(summary.plannedExpenses, currency)} − факт ${formatMoney(summary.spent, currency)}`}
          accent
        />
        <StatCard
          label="Дневной бюджет"
          amount={summary.dailyBudgetAtStart}
          currency={currency}
          hint={`На период ${summary.daysInPeriod} дн. · осталось ${summary.daysRemaining} дн.`}
        />
        <StatCard
          label="Доступно сегодня"
          amount={summary.availableToday}
          currency={currency}
          hint={`Потрачено сегодня: ${formatMoney(summary.spentToday, currency)}`}
        />
      </div>

      {byCategory.length > 0 && (
        <Card>
          <CardTitle>Расходы по категориям</CardTitle>
          <ul className="mt-4 divide-y divide-slate-100">
            {byCategory.map(([name, total]) => (
              <li key={name} className="flex items-center justify-between py-2 text-sm">
                <span className="text-slate-700">{name}</span>
                <span className="font-medium text-slate-900">{formatMoney(total, currency)}</span>
              </li>
            ))}
          </ul>
        </Card>
      )}

      <Card>
        <CardTitle>Быстрый расход</CardTitle>
        <form onSubmit={(e) => void handleSubmit(e)} className="mt-4 space-y-4">
          <div className="grid gap-4 sm:grid-cols-2">
            <Input
              label="Сумма"
              type="text"
              inputMode="decimal"
              placeholder="0"
              value={amount}
              onChange={(e) => setAmount(e.target.value)}
              required
            />
            <Select
              label="Категория"
              value={categoryId}
              onChange={(e) => setCategoryId(e.target.value)}
              options={categoryOptions}
            />
          </div>
          <Input
            label="Комментарий"
            placeholder="За что потратили?"
            value={note}
            onChange={(e) => setNote(e.target.value)}
          />
          <Button
            type="submit"
            loading={createTransaction.isPending}
            className="w-full sm:w-auto"
          >
            Добавить расход
          </Button>
        </form>
      </Card>

      <MobileMoreMenu />
    </div>
  )
}
