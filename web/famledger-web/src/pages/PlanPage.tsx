import { useMemo, useState, type FormEvent, type ReactNode } from 'react'
import {
  useCreateIncome,
  useCreateOneOff,
  useCreateRecurring,
  useDeleteIncome,
  useDeleteOneOff,
  useDeleteRecurring,
  useIncomes,
  useOneOffExpenses,
  usePermissions,
  useRecurringExpenses,
  useSettings,
  useToggleOneOffPaid,
  useToggleRecurringPaid,
  useToggleRecurringSkip,
  useUpdateIncome,
  useUpdateRecurring,
} from '../api/hooks'
import { currencyOptions } from '../api/types'
import { Card } from '../components/ui/Card'
import { EmptyState, PageHeader, Spinner, Tabs } from '../components/ui/Tabs'
import { MoneyDisplay } from '../components/ui/MoneyDisplay'
import { Badge } from '../components/ui/Tabs'
import { Button } from '../components/ui/Button'
import { useConfirmDialog } from '../components/ui/ConfirmDialog'
import { Input, Select } from '../components/ui/Input'
import { formatMoney } from '../lib/format'

const planTabs = [
  { id: 'recurring', label: 'Постоянные' },
  { id: 'monthly', label: 'Этот месяц' },
  { id: 'incomes', label: 'Доходы' },
]

export function PlanPage() {
  const [activeTab, setActiveTab] = useState('recurring')
  const { confirm } = useConfirmDialog()
  const { data: settings } = useSettings()
  const { canManagePlan } = usePermissions()
  const defaultCurrency = settings?.baseCurrency ?? 'RSD'

  const recurring = useRecurringExpenses()
  const oneOff = useOneOffExpenses()
  const incomes = useIncomes()

  const createRecurring = useCreateRecurring()
  const updateRecurring = useUpdateRecurring()
  const deleteRecurring = useDeleteRecurring()
  const toggleRecurring = useToggleRecurringPaid()
  const toggleRecurringSkip = useToggleRecurringSkip()
  const createOneOff = useCreateOneOff()
  const deleteOneOff = useDeleteOneOff()
  const toggleOneOff = useToggleOneOffPaid()
  const createIncome = useCreateIncome()
  const updateIncome = useUpdateIncome()
  const deleteIncome = useDeleteIncome()

  const [editingRecurringId, setEditingRecurringId] = useState<string | null>(null)
  const [editingIncomeId, setEditingIncomeId] = useState<string | null>(null)

  const isLoading =
    (activeTab === 'recurring' && recurring.isLoading) ||
    (activeTab === 'monthly' && oneOff.isLoading) ||
    (activeTab === 'incomes' && incomes.isLoading)

  const recurringTotalRsd = useMemo(
    () =>
      (recurring.data ?? [])
        .filter((i) => !i.isSkipped)
        .reduce((sum, i) => sum + (i.plannedBaseAmount ?? i.periodAmount ?? 0), 0),
    [recurring.data],
  )
  const oneOffTotalRsd = useMemo(
    () => (oneOff.data ?? []).reduce((sum, i) => sum + (i.baseAmount ?? 0), 0),
    [oneOff.data],
  )
  const incomesTotalRsd = useMemo(
    () => (incomes.data ?? []).reduce((sum, i) => sum + (i.baseAmount ?? 0), 0),
    [incomes.data],
  )

  return (
    <div className="space-y-6">
      <PageHeader
        title="План"
        subtitle={
          canManagePlan
            ? 'Постоянные расходы, разовые траты и доходы периода'
            : 'Просмотр плана (редактирование доступно главе и помощнику)'
        }
      />

      <Tabs tabs={planTabs} activeTab={activeTab} onChange={setActiveTab} />

      {isLoading ? (
        <div className="flex justify-center py-16">
          <Spinner />
        </div>
      ) : (
        <>
          {activeTab === 'recurring' && (
            <>
              <PlanTotal label="Итого постоянные" amount={recurringTotalRsd} currency={defaultCurrency} />
              {canManagePlan && (
                <AddRecurringForm
                  defaultCurrency={defaultCurrency}
                  loading={createRecurring.isPending}
                  onSubmit={(payload) => createRecurring.mutateAsync(payload)}
                />
              )}
              <PlanList
                emptyTitle="Нет постоянных расходов"
                emptyDescription="Добавьте подписки, аренду и другие регулярные платежи."
                items={recurring.data ?? []}
                renderItem={(item) => {
                  const expenseId = item.recurringExpenseId ?? item.id
                  if (editingRecurringId === expenseId && canManagePlan) {
                    return (
                      <EditRecurringForm
                        item={item}
                        loading={updateRecurring.isPending}
                        onCancel={() => setEditingRecurringId(null)}
                        onSave={async (payload) => {
                          await updateRecurring.mutateAsync({ id: expenseId, ...payload })
                          setEditingRecurringId(null)
                        }}
                      />
                    )
                  }
                  return (
                    <div className={`flex items-center justify-between gap-3 ${item.isSkipped ? 'opacity-60' : ''}`}>
                      <div>
                        <p className={`font-medium text-slate-900 ${item.isSkipped ? 'line-through' : ''}`}>
                          {item.name}
                        </p>
                        <p className="text-sm text-slate-500">
                          {item.chargeDayOfMonth}-е число · {item.definitionCurrency}
                          {item.isSkipped ? (
                            <>
                              {' · '}
                              <Badge variant="default">Нет в этом месяце</Badge>
                            </>
                          ) : item.isPaid !== undefined ? (
                            <>
                              {' · '}
                              <Badge variant={item.isPaid ? 'success' : 'warning'}>
                                {item.isPaid ? 'Оплачено' : 'Не оплачено'}
                              </Badge>
                            </>
                          ) : null}
                        </p>
                      </div>
                      <div className="flex flex-wrap items-center justify-end gap-2">
                        <MoneyDisplay
                          amount={item.definitionAmount}
                          currency={item.definitionCurrency}
                        />
                        {canManagePlan && (
                          <>
                            <Button
                              variant="ghost"
                              size="sm"
                              onClick={() => void toggleRecurringSkip.mutateAsync(item.id)}
                            >
                              {item.isSkipped ? 'Вернуть' : 'Нет в этом месяце'}
                            </Button>
                            {!item.isSkipped && (
                              <Button
                                variant="ghost"
                                size="sm"
                                onClick={() => void toggleRecurring.mutateAsync(item.id)}
                              >
                                {item.isPaid ? 'Снять' : 'Оплатить'}
                              </Button>
                            )}
                            <Button
                              variant="ghost"
                              size="sm"
                              onClick={() => setEditingRecurringId(expenseId)}
                            >
                              Изменить
                            </Button>
                            <Button
                              variant="ghost"
                              size="sm"
                              className="text-red-600 hover:bg-red-50"
                              onClick={async () => {
                                const accepted = await confirm({
                                  title: `Удалить «${item.name}»?`,
                                  message: 'Постоянный расход будет удалён из плана.',
                                })
                                if (accepted) {
                                  void deleteRecurring.mutateAsync(expenseId)
                                }
                              }}
                            >
                              Удалить
                            </Button>
                          </>
                        )}
                      </div>
                    </div>
                  )
                }}
              />
            </>
          )}

          {activeTab === 'monthly' && (
            <>
              <PlanTotal label="Итого разовые" amount={oneOffTotalRsd} currency={defaultCurrency} />
              {canManagePlan && (
                <AddOneOffForm
                  defaultCurrency={defaultCurrency}
                  loading={createOneOff.isPending}
                  onSubmit={(payload) => createOneOff.mutateAsync(payload)}
                />
              )}
              <PlanList
                emptyTitle="Нет разовых расходов"
                emptyDescription="Запланируйте крупные траты на этот период."
                items={oneOff.data ?? []}
                renderItem={(item) => (
                  <div className="flex items-center justify-between gap-3">
                    <div>
                      <p className="font-medium text-slate-900">{item.name}</p>
                      <Badge variant={item.isPaid ? 'success' : 'warning'}>
                        {item.isPaid ? 'Оплачено' : 'Не оплачено'}
                      </Badge>
                    </div>
                    <div className="flex items-center gap-2">
                      <MoneyDisplay amount={item.amount} currency={item.currency} />
                      {canManagePlan && (
                        <>
                          <Button
                            variant="ghost"
                            size="sm"
                            onClick={() => void toggleOneOff.mutateAsync(item.id)}
                          >
                            {item.isPaid ? 'Снять' : 'Оплатить'}
                          </Button>
                          <Button
                            variant="ghost"
                            size="sm"
                            className="text-red-600 hover:bg-red-50"
                            onClick={async () => {
                              const accepted = await confirm({
                                title: `Удалить «${item.name}»?`,
                                message: 'Разовая трата будет удалена из текущего периода.',
                              })
                              if (accepted) {
                                void deleteOneOff.mutateAsync(item.id)
                              }
                            }}
                          >
                            Удалить
                          </Button>
                        </>
                      )}
                    </div>
                  </div>
                )}
              />
            </>
          )}

          {activeTab === 'incomes' && (
            <>
              <PlanTotal label="Итого доходы" amount={incomesTotalRsd} currency={defaultCurrency} />
              {canManagePlan && (
                <AddIncomeForm
                  defaultCurrency={defaultCurrency}
                  loading={createIncome.isPending}
                  onSubmit={(payload) => createIncome.mutateAsync(payload)}
                />
              )}
              <PlanList
                emptyTitle="Нет доходов"
                emptyDescription="Добавьте зарплату и другие источники дохода."
                items={incomes.data ?? []}
                renderItem={(item) => {
                  if (editingIncomeId === item.id && canManagePlan) {
                    return (
                      <EditIncomeForm
                        item={item}
                        loading={updateIncome.isPending}
                        onCancel={() => setEditingIncomeId(null)}
                        onSave={async (payload) => {
                          await updateIncome.mutateAsync({ id: item.id, ...payload })
                          setEditingIncomeId(null)
                        }}
                      />
                    )
                  }
                  return (
                    <div className="flex items-center justify-between gap-3">
                      <p className="font-medium text-slate-900">{item.name}</p>
                      <div className="flex items-center gap-2">
                        <MoneyDisplay amount={item.amount} currency={item.currency} signed />
                        {canManagePlan && (
                          <>
                            <Button
                              variant="ghost"
                              size="sm"
                              onClick={() => setEditingIncomeId(item.id)}
                            >
                              Изменить
                            </Button>
                            <Button
                              variant="ghost"
                              size="sm"
                              className="text-red-600 hover:bg-red-50"
                              onClick={async () => {
                                const accepted = await confirm({
                                  title: `Удалить «${item.name}»?`,
                                  message: 'Источник дохода будет удалён из плана.',
                                })
                                if (accepted) {
                                  void deleteIncome.mutateAsync(item.id)
                                }
                              }}
                            >
                              Удалить
                            </Button>
                          </>
                        )}
                      </div>
                    </div>
                  )
                }}
              />
            </>
          )}
        </>
      )}
    </div>
  )
}

function PlanTotal({
  label,
  amount,
  currency,
}: {
  label: string
  amount: number
  currency: string
}) {
  return (
    <div className="flex items-center justify-between rounded-2xl border border-slate-200/80 bg-white px-5 py-4 shadow-sm">
      <p className="text-sm font-medium text-slate-600">{label}</p>
      <p className="text-lg font-bold tabular-nums text-slate-900">
        {formatMoney(amount, currency)}
      </p>
    </div>
  )
}

function AddRecurringForm({
  defaultCurrency,
  loading,
  onSubmit,
}: {
  defaultCurrency: string
  loading: boolean
  onSubmit: (payload: {
    name: string
    amount: number
    currency: string
    chargeDay: number
  }) => Promise<unknown>
}) {
  const [name, setName] = useState('')
  const [amount, setAmount] = useState('')
  const [currency, setCurrency] = useState(defaultCurrency)
  const [chargeDay, setChargeDay] = useState('1')

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    const parsed = Number.parseFloat(amount.replace(',', '.'))
    const day = Number.parseInt(chargeDay, 10)
    if (!name.trim() || Number.isNaN(parsed) || parsed <= 0 || day < 1 || day > 28) return
    await onSubmit({ name: name.trim(), amount: parsed, currency, chargeDay: day })
    setName('')
    setAmount('')
  }

  return (
    <Card>
      <form className="grid gap-3 sm:grid-cols-2 lg:grid-cols-5 lg:items-end" onSubmit={(e) => void handleSubmit(e)}>
        <Input label="Название" value={name} onChange={(e) => setName(e.target.value)} placeholder="Аренда" />
        <Input label="Сумма" value={amount} onChange={(e) => setAmount(e.target.value)} placeholder="500" />
        <Select label="Валюта" value={currency} onChange={(e) => setCurrency(e.target.value)} options={currencyOptions} />
        <Input label="День" value={chargeDay} onChange={(e) => setChargeDay(e.target.value)} placeholder="1" />
        <Button type="submit" loading={loading} className="shrink-0">
          Добавить
        </Button>
      </form>
    </Card>
  )
}

function EditRecurringForm({
  item,
  loading,
  onSave,
  onCancel,
}: {
  item: { name: string; definitionAmount: number; definitionCurrency: string; chargeDayOfMonth: number }
  loading: boolean
  onSave: (payload: { name: string; amount: number; currency: string; chargeDay: number }) => Promise<void>
  onCancel: () => void
}) {
  const [name, setName] = useState(item.name)
  const [amount, setAmount] = useState(String(item.definitionAmount))
  const [currency, setCurrency] = useState(item.definitionCurrency)
  const [chargeDay, setChargeDay] = useState(String(item.chargeDayOfMonth))

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    const parsed = Number.parseFloat(amount.replace(',', '.'))
    const day = Number.parseInt(chargeDay, 10)
    if (!name.trim() || Number.isNaN(parsed) || parsed <= 0) return
    await onSave({ name: name.trim(), amount: parsed, currency, chargeDay: day })
  }

  return (
    <form className="grid gap-2 sm:grid-cols-2 lg:grid-cols-5 lg:items-end" onSubmit={(e) => void handleSubmit(e)}>
      <Input label="Название" value={name} onChange={(e) => setName(e.target.value)} />
      <Input label="Сумма" value={amount} onChange={(e) => setAmount(e.target.value)} />
      <Select label="Валюта" value={currency} onChange={(e) => setCurrency(e.target.value)} options={currencyOptions} />
      <Input label="День" value={chargeDay} onChange={(e) => setChargeDay(e.target.value)} />
      <div className="flex gap-2">
        <Button type="submit" size="sm" loading={loading}>
          Сохранить
        </Button>
        <Button type="button" size="sm" variant="ghost" onClick={onCancel}>
          Отмена
        </Button>
      </div>
    </form>
  )
}

function AddOneOffForm({
  defaultCurrency,
  loading,
  onSubmit,
}: {
  defaultCurrency: string
  loading: boolean
  onSubmit: (payload: { name: string; amount: number; currency: string }) => Promise<unknown>
}) {
  const [name, setName] = useState('')
  const [amount, setAmount] = useState('')
  const [currency, setCurrency] = useState(defaultCurrency)

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    const parsed = Number.parseFloat(amount.replace(',', '.'))
    if (!name.trim() || Number.isNaN(parsed) || parsed <= 0) return
    await onSubmit({ name: name.trim(), amount: parsed, currency })
    setName('')
    setAmount('')
  }

  return (
    <Card>
      <form className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4 lg:items-end" onSubmit={(e) => void handleSubmit(e)}>
        <Input label="Название" value={name} onChange={(e) => setName(e.target.value)} placeholder="Подарок" />
        <Input label="Сумма" value={amount} onChange={(e) => setAmount(e.target.value)} placeholder="50" />
        <Select label="Валюта" value={currency} onChange={(e) => setCurrency(e.target.value)} options={currencyOptions} />
        <Button type="submit" loading={loading} className="shrink-0">
          Добавить
        </Button>
      </form>
    </Card>
  )
}

function AddIncomeForm({
  defaultCurrency,
  loading,
  onSubmit,
}: {
  defaultCurrency: string
  loading: boolean
  onSubmit: (payload: { name: string; amount: number; currency: string }) => Promise<unknown>
}) {
  const [name, setName] = useState('')
  const [amount, setAmount] = useState('')
  const [currency, setCurrency] = useState(defaultCurrency)

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    const parsed = Number.parseFloat(amount.replace(',', '.'))
    if (!name.trim() || Number.isNaN(parsed) || parsed <= 0) return
    await onSubmit({ name: name.trim(), amount: parsed, currency })
    setName('')
    setAmount('')
  }

  return (
    <Card>
      <form className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4 lg:items-end" onSubmit={(e) => void handleSubmit(e)}>
        <Input label="Название" value={name} onChange={(e) => setName(e.target.value)} placeholder="Зарплата" />
        <Input label="Сумма" value={amount} onChange={(e) => setAmount(e.target.value)} placeholder="1000" />
        <Select label="Валюта" value={currency} onChange={(e) => setCurrency(e.target.value)} options={currencyOptions} />
        <Button type="submit" loading={loading} className="shrink-0">
          Добавить
        </Button>
      </form>
    </Card>
  )
}

function EditIncomeForm({
  item,
  loading,
  onSave,
  onCancel,
}: {
  item: { name: string; amount: number; currency: string }
  loading: boolean
  onSave: (payload: { name: string; amount: number; currency: string }) => Promise<void>
  onCancel: () => void
}) {
  const [name, setName] = useState(item.name)
  const [amount, setAmount] = useState(String(item.amount))
  const [currency, setCurrency] = useState(item.currency)

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    const parsed = Number.parseFloat(amount.replace(',', '.'))
    if (!name.trim() || Number.isNaN(parsed) || parsed <= 0) return
    await onSave({ name: name.trim(), amount: parsed, currency })
  }

  return (
    <form className="grid gap-2 sm:grid-cols-2 lg:grid-cols-4 lg:items-end" onSubmit={(e) => void handleSubmit(e)}>
      <Input label="Название" value={name} onChange={(e) => setName(e.target.value)} />
      <Input label="Сумма" value={amount} onChange={(e) => setAmount(e.target.value)} />
      <Select label="Валюта" value={currency} onChange={(e) => setCurrency(e.target.value)} options={currencyOptions} />
      <div className="flex gap-2">
        <Button type="submit" size="sm" loading={loading}>
          Сохранить
        </Button>
        <Button type="button" size="sm" variant="ghost" onClick={onCancel}>
          Отмена
        </Button>
      </div>
    </form>
  )
}

function PlanList<T extends { id: string }>({
  items,
  renderItem,
  emptyTitle,
  emptyDescription,
}: {
  items: T[]
  renderItem: (item: T) => ReactNode
  emptyTitle: string
  emptyDescription: string
}) {
  if (items.length === 0) {
    return <EmptyState title={emptyTitle} description={emptyDescription} />
  }

  return (
    <Card padding="none">
      <ul className="divide-y divide-slate-100">
        {items.map((item) => (
          <li key={item.id} className="px-5 py-4">
            {renderItem(item)}
          </li>
        ))}
      </ul>
    </Card>
  )
}
