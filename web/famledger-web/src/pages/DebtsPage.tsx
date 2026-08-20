import { useState, type FormEvent } from 'react'
import {
  useAddDebtEntry,
  useCreateDebt,
  useDebts,
  useDeleteDebt,
  useDeleteDebtEntry,
  useFamily,
  useSettings,
  useToggleDebtPaid,
} from '../api/hooks'
import type { DebtDirection } from '../api/types'
import { Card } from '../components/ui/Card'
import { EmptyState, PageHeader, Spinner, Badge } from '../components/ui/Tabs'
import { MoneyDisplay } from '../components/ui/MoneyDisplay'
import { Button } from '../components/ui/Button'
import { useConfirmDialog } from '../components/ui/ConfirmDialog'
import { Input, Select } from '../components/ui/Input'
import { formatDateTime } from '../lib/format'

export function DebtsPage() {
  const [hidePaid, setHidePaid] = useState(false)
  const { confirm } = useConfirmDialog()
  const { data: settings } = useSettings()
  const { data: family } = useFamily()
  const { data: debts, isLoading, isError, refetch } = useDebts(hidePaid)
  const createDebt = useCreateDebt()
  const addEntry = useAddDebtEntry()
  const togglePaid = useToggleDebtPaid()
  const deleteDebt = useDeleteDebt()
  const deleteEntry = useDeleteDebtEntry()

  const [showForm, setShowForm] = useState(false)
  const [name, setName] = useState('')
  const [direction, setDirection] = useState<DebtDirection>('WeOwe')
  const [memberUserId, setMemberUserId] = useState('')
  const [expandedDebtId, setExpandedDebtId] = useState<string | null>(null)
  const [entryAmount, setEntryAmount] = useState('')
  const [entryDesc, setEntryDesc] = useState('')

  const currency = settings?.baseCurrency ?? 'RSD'
  const members = family?.members ?? []

  async function handleCreateDebt(event: FormEvent) {
    event.preventDefault()
    const counterpartyName =
      memberUserId
        ? members.find((m) => m.userId === memberUserId)?.displayName ?? name.trim()
        : name.trim()
    if (!counterpartyName) return

    await createDebt.mutateAsync({
      counterpartyName,
      counterpartyUserId: memberUserId || undefined,
      direction,
    })
    setName('')
    setMemberUserId('')
    setShowForm(false)
  }

  async function handleAddEntry(debtId: string) {
    const parsed = Number.parseFloat(entryAmount.replace(',', '.'))
    if (Number.isNaN(parsed) || parsed <= 0) return
    await addEntry.mutateAsync({
      debtId,
      amount: parsed,
      currency,
      description: entryDesc.trim() || undefined,
    })
    setEntryAmount('')
    setEntryDesc('')
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
        title="Не удалось загрузить долги"
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
        title="Долги"
        subtitle="Кто кому должен в семье и с окружением"
        action={
          <Button onClick={() => setShowForm((v) => !v)}>
            {showForm ? 'Отмена' : 'Добавить долг'}
          </Button>
        }
      />

      <label className="flex items-center gap-2 text-sm text-slate-600">
        <input
          type="checkbox"
          checked={hidePaid}
          onChange={(e) => setHidePaid(e.target.checked)}
          className="rounded border-slate-300"
        />
        Скрыть полностью погашенные
      </label>

      {showForm && (
        <Card>
          <form className="space-y-3" onSubmit={(e) => void handleCreateDebt(e)}>
            <Input
              label="Имя контрагента"
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder="Иван или компания"
              disabled={Boolean(memberUserId)}
            />
            {members.length > 0 && (
              <Select
                label="Или участник семьи"
                value={memberUserId}
                onChange={(e) => setMemberUserId(e.target.value)}
                options={[
                  { value: '', label: '— Внешний контрагент —' },
                  ...members.map((m) => ({
                    value: m.userId,
                    label: m.displayName,
                  })),
                ]}
              />
            )}
            <Select
              label="Направление"
              value={direction}
              onChange={(e) => setDirection(e.target.value as DebtDirection)}
              options={[
                { value: 'WeOwe', label: 'Мы должны' },
                { value: 'OwedToUs', label: 'Нам должны' },
              ]}
            />
            <Button type="submit" loading={createDebt.isPending}>
              Создать
            </Button>
          </form>
        </Card>
      )}

      {!debts?.length ? (
        <EmptyState
          title="Долгов пока нет"
          description="Отслеживайте займы между членами семьи и внешними контрагентами."
          action={<Button onClick={() => setShowForm(true)}>Добавить долг</Button>}
        />
      ) : (
        <div className="space-y-3">
          {debts.map((debt) => (
            <Card key={debt.id} padding="none">
              <button
                type="button"
                className="flex w-full items-center justify-between gap-4 px-5 py-4 text-left"
                onClick={() =>
                  setExpandedDebtId((id) => (id === debt.id ? null : debt.id))
                }
              >
                <div>
                  <p className="font-medium text-slate-900">{debt.counterpartyName}</p>
                  <Badge variant={debt.direction === 'OwedToUs' ? 'success' : 'warning'}>
                    {debt.direction === 'OwedToUs' ? 'Нам должны' : 'Мы должны'}
                  </Badge>
                </div>
                <MoneyDisplay
                  amount={debt.balance}
                  currency={debt.currency}
                  signed={debt.direction === 'OwedToUs'}
                />
              </button>

              {expandedDebtId === debt.id && (
                <div className="border-t border-slate-100 px-5 py-4 space-y-4">
                  {(debt.entries ?? []).length > 0 && (
                    <ul className="divide-y divide-slate-100 rounded-xl border border-slate-200">
                      {(debt.entries ?? []).map((entry) => (
                        <li
                          key={entry.id}
                          className="flex items-center justify-between gap-3 px-4 py-3"
                        >
                          <div>
                            <p className="font-medium text-slate-900">
                              {entry.description || 'Запись'}
                            </p>
                            <p className="text-xs text-slate-500">
                              {formatDateTime(entry.createdAt)}
                            </p>
                          </div>
                          <div className="flex items-center gap-2">
                            <MoneyDisplay amount={entry.amount} currency={entry.currency} />
                            <Button
                              size="sm"
                              variant="ghost"
                              onClick={() => void togglePaid.mutateAsync(entry.id)}
                            >
                              {entry.isPaid ? 'Снять' : 'Оплачено'}
                            </Button>
                            <Button
                              size="sm"
                              variant="ghost"
                              className="text-red-600 hover:bg-red-50"
                              loading={deleteEntry.isPending}
                              onClick={async () => {
                                const accepted = await confirm({
                                  title: 'Удалить запись долга?',
                                  message: 'Эта запись исчезнет из истории долга.',
                                })
                                if (accepted) {
                                  void deleteEntry.mutateAsync(entry.id)
                                }
                              }}
                            >
                              Удалить
                            </Button>
                          </div>
                        </li>
                      ))}
                    </ul>
                  )}

                  <div className="flex flex-col gap-2 sm:flex-row sm:items-end">
                    <Input
                      label="Сумма записи"
                      value={entryAmount}
                      onChange={(e) => setEntryAmount(e.target.value)}
                      placeholder="100"
                    />
                    <Input
                      label="Описание"
                      value={entryDesc}
                      onChange={(e) => setEntryDesc(e.target.value)}
                      placeholder="За обед"
                    />
                    <Button
                      loading={addEntry.isPending}
                      onClick={() => void handleAddEntry(debt.id)}
                    >
                      Добавить запись
                    </Button>
                  </div>

                  <Button
                    variant="ghost"
                    size="sm"
                    className="text-red-600 hover:bg-red-50"
                    loading={deleteDebt.isPending}
                    onClick={async () => {
                      const accepted = await confirm({
                        title: `Удалить долг «${debt.counterpartyName}»?`,
                        message: 'Будут удалены и сам долг, и все его записи.',
                      })
                      if (accepted) {
                        void deleteDebt.mutateAsync(debt.id)
                        setExpandedDebtId(null)
                      }
                    }}
                  >
                    Удалить долг
                  </Button>
                </div>
              )}
            </Card>
          ))}
        </div>
      )}
    </div>
  )
}
