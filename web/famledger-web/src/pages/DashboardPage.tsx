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
import { EmptyState, PageHeader, Spinner, Tabs } from '../components/ui/Tabs'
import { StatCard } from '../components/ui/MoneyDisplay'
import { MobileMoreMenu } from '../components/layout/Navigation'
import { formatMoney } from '../lib/format'
import { currencyOptions } from '../api/types'
import type { FormEvent } from 'react'

type TxMode = 'Expense' | 'Income'

export function DashboardPage() {
  const { data: summary, isLoading, isError, refetch } = useDashboard()
  const { data: categories } = useCategories()
  const { data: transactions } = useTransactions()
  const createTransaction = useCreateTransaction()

  const [mode, setMode] = useState<TxMode>('Expense')
  const [amount, setAmount] = useState('')
  const [expenseCurrency, setExpenseCurrency] = useState('')
  const [categoryId, setCategoryId] = useState('')
  const [note, setNote] = useState('')

  const currency = summary?.currency ?? 'RSD'
  const selectedCurrency = expenseCurrency || currency

  const byCategory = useMemo(() => {
    if (!transactions?.length) return []
    const map = new Map<string, number>()
    for (const tx of transactions) {
      if ((tx.kind ?? 'Expense') !== 'Expense') continue
      const key = tx.categoryName ?? 'Без категории'
      map.set(key, (map.get(key) ?? 0) + tx.baseAmount)
    }
    return [...map.entries()].sort((a, b) => b[1] - a[1])
  }, [transactions])

  const filteredCategories = useMemo(() => {
    const kind = mode
    return (
      categories?.filter((c) => {
        const k = c.kind ?? 'Expense'
        return k === kind
      }) ?? []
    )
  }, [categories, mode])

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    const parsed = Number.parseFloat(amount.replace(',', '.'))
    if (Number.isNaN(parsed) || parsed <= 0) return

    await createTransaction.mutateAsync({
      amount: parsed,
      currency: selectedCurrency,
      categoryId: categoryId || undefined,
      note: note.trim() || undefined,
      kind: mode,
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
    ...filteredCategories.map((c) => ({
      value: c.id,
      label: c.emoji ? `${c.emoji} ${c.name}` : c.name,
    })),
  ]

  const breakdown = [
    { label: 'Доходы', amount: summary.income },
    ...(summary.topUps > 0
      ? [{ label: 'Пополнения', amount: summary.topUps }]
      : []),
    { label: 'План', amount: summary.plannedExpenses },
    { label: 'Факт', amount: summary.spent },
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
          accent
          breakdown={breakdown}
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
        <CardTitle>Быстрая операция</CardTitle>
        <form onSubmit={(e) => void handleSubmit(e)} className="mt-4 space-y-4">
          <Tabs
            tabs={[
              { id: 'Expense', label: 'Списание' },
              { id: 'Income', label: 'Пополнение' },
            ]}
            activeTab={mode}
            onChange={(id) => {
              setMode(id as TxMode)
              setCategoryId('')
            }}
          />
          <div className="grid gap-4 sm:grid-cols-3">
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
              label="Валюта"
              value={selectedCurrency}
              onChange={(e) => setExpenseCurrency(e.target.value)}
              options={currencyOptions}
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
            placeholder={mode === 'Income' ? 'Откуда деньги?' : 'За что потратили?'}
            value={note}
            onChange={(e) => setNote(e.target.value)}
          />
          <Button
            type="submit"
            loading={createTransaction.isPending}
            className="w-full sm:w-auto"
          >
            {mode === 'Income' ? 'Добавить пополнение' : 'Добавить расход'}
          </Button>
        </form>
      </Card>

      <MobileMoreMenu />
    </div>
  )
}
