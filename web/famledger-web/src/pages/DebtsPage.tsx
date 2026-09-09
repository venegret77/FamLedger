import { useMemo, useState, type FormEvent } from 'react'
import {
  useAddDebtEntry,
  useCreateDebt,
  useDebts,
  useDeleteDebt,
  useDeleteDebtEntry,
  useFamily,
  usePayDebtEntry,
  useSettings,
  useToggleDebtPaid,
} from '../api/hooks'
import type { Debt, DebtDirection, DebtEntry } from '../api/types'
import { Card } from '../components/ui/Card'
import { EmptyState, PageHeader, Spinner, Badge } from '../components/ui/Tabs'
import { MoneyDisplay } from '../components/ui/MoneyDisplay'
import { Button } from '../components/ui/Button'
import { useConfirmDialog } from '../components/ui/ConfirmDialog'
import { Input, Select } from '../components/ui/Input'
import { formatDateTime, formatMoney } from '../lib/format'

const NEW_COUNTERPARTY = '__new__'

function entryRemaining(entry: DebtEntry): number {
  if (entry.remainingAmount != null) return entry.remainingAmount
  if (entry.isPaid) return 0
  return Math.max(0, entry.amount - (entry.paidAmount ?? 0))
}

function debtStatusLabel(debt: Debt): string {
  if (debt.direction === 'Mutual') return 'Взаимный долг'
  if (debt.direction === 'OwedToUs') return 'Нам должны'
  return 'Мы должны'
}

function debtStatusVariant(debt: Debt): 'success' | 'warning' | 'default' {
  if (debt.direction === 'Mutual') return 'default'
  if (debt.direction === 'OwedToUs') return 'success'
  return 'warning'
}

function netSignedAmount(debt: Debt): number {
  if (debt.netBalance != null) return debt.netBalance
  if (debt.direction === 'OwedToUs') return debt.balance
  if (debt.direction === 'WeOwe') return -debt.balance
  return (debt.owedToUs ?? 0) - (debt.weOwe ?? 0)
}

export function DebtsPage() {
  const [hidePaid, setHidePaid] = useState(false)
  const { confirm } = useConfirmDialog()
  const { data: settings } = useSettings()
  const { data: family } = useFamily()
  const { data: debts, isLoading, isError, refetch } = useDebts(hidePaid)
  const { data: allDebts } = useDebts(false)
  const createDebt = useCreateDebt()
  const addEntry = useAddDebtEntry()
  const togglePaid = useToggleDebtPaid()
  const payEntry = usePayDebtEntry()
  const deleteDebt = useDeleteDebt()
  const deleteEntry = useDeleteDebtEntry()

  const [showForm, setShowForm] = useState(false)
  const [counterpartyMode, setCounterpartyMode] = useState(NEW_COUNTERPARTY)
  const [name, setName] = useState('')
  const [direction, setDirection] = useState<DebtDirection>('WeOwe')
  const [memberUserId, setMemberUserId] = useState('')
  const [expandedDebtId, setExpandedDebtId] = useState<string | null>(null)
  const [entryAmount, setEntryAmount] = useState('')
  const [entryDesc, setEntryDesc] = useState('')
  const [entryDirection, setEntryDirection] = useState<DebtDirection>('WeOwe')
  const [payingEntryId, setPayingEntryId] = useState<string | null>(null)
  const [payAmount, setPayAmount] = useState('')

  const currency = settings?.baseCurrency ?? 'RSD'
  const members = family?.members ?? []

  const existingCounterparties = useMemo(() => {
    const seen = new Set<string>()
    const items: { key: string; label: string; name: string; userId?: string }[] = []
    for (const debt of allDebts ?? debts ?? []) {
      const key = debt.counterpartyUserId
        ? `user:${debt.counterpartyUserId}`
        : `name:${debt.counterpartyName.trim().toLowerCase()}`
      if (seen.has(key)) continue
      seen.add(key)
      items.push({
        key,
        label: debt.counterpartyName,
        name: debt.counterpartyName,
        userId: debt.counterpartyUserId,
      })
    }
    return items.sort((a, b) => a.label.localeCompare(b.label, 'ru'))
  }, [allDebts, debts])

  const selectedExisting = existingCounterparties.find((c) => c.key === counterpartyMode)
  const isNewCounterparty = counterpartyMode === NEW_COUNTERPARTY

  async function handleCreateDebt(event: FormEvent) {
    event.preventDefault()

    let counterpartyName = name.trim()
    let counterpartyUserId: string | undefined

    if (!isNewCounterparty && selectedExisting) {
      counterpartyName = selectedExisting.name
      counterpartyUserId = selectedExisting.userId
    } else if (memberUserId) {
      counterpartyName =
        members.find((m) => m.userId === memberUserId)?.displayName ?? counterpartyName
      counterpartyUserId = memberUserId
    }

    if (!counterpartyName) return

    await createDebt.mutateAsync({
      counterpartyName,
      counterpartyUserId,
      direction,
    })
    setName('')
    setMemberUserId('')
    setCounterpartyMode(NEW_COUNTERPARTY)
    setShowForm(false)
    setExpandedDebtId(
      counterpartyUserId
        ? `user:${counterpartyUserId}`
        : `name:${counterpartyName.trim().toLowerCase()}`,
    )
  }

  async function resolveLegId(debt: Debt, legDirection: DebtDirection): Promise<string> {
    const existing = debt.legs?.find((leg) => leg.direction === legDirection)
    if (existing) return existing.id

    const created = await createDebt.mutateAsync({
      counterpartyName: debt.counterpartyName,
      counterpartyUserId: debt.counterpartyUserId,
      direction: legDirection,
    })
    return created.id
  }

  async function handleAddEntry(debt: Debt) {
    const parsed = Number.parseFloat(entryAmount.replace(',', '.'))
    if (Number.isNaN(parsed) || parsed <= 0) return

    const debtId = await resolveLegId(debt, entryDirection)
    await addEntry.mutateAsync({
      debtId,
      amount: parsed,
      currency,
      description: entryDesc.trim() || undefined,
    })
    setEntryAmount('')
    setEntryDesc('')
  }

  function startPayment(entry: DebtEntry) {
    const remaining = entryRemaining(entry)
    setPayingEntryId(entry.id)
    setPayAmount(String(remaining))
  }

  async function submitPayment(entry: DebtEntry) {
    const parsed = Number.parseFloat(payAmount.replace(',', '.'))
    const remaining = entryRemaining(entry)
    if (Number.isNaN(parsed) || parsed <= 0 || parsed > remaining) return

    await payEntry.mutateAsync({ entryId: entry.id, amount: parsed })
    setPayingEntryId(null)
    setPayAmount('')
  }

  async function deleteDebtGroup(debt: Debt) {
    const accepted = await confirm({
      title: `Удалить долг «${debt.counterpartyName}»?`,
      message: 'Будут удалены все записи по этому человеку в обоих направлениях.',
    })
    if (!accepted) return

    const legIds = debt.legs?.map((leg) => leg.id) ?? [debt.id]
    for (const legId of legIds) {
      await deleteDebt.mutateAsync(legId)
    }
    setExpandedDebtId(null)
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
            <Select
              label="Контрагент"
              value={counterpartyMode}
              onChange={(e) => {
                setCounterpartyMode(e.target.value)
                setMemberUserId('')
                setName('')
              }}
              options={[
                { value: NEW_COUNTERPARTY, label: '— Новый контрагент —' },
                ...existingCounterparties.map((c) => ({
                  value: c.key,
                  label: c.label,
                })),
              ]}
            />

            {isNewCounterparty && (
              <>
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
              </>
            )}

            {!isNewCounterparty && selectedExisting && (
              <p className="text-sm text-slate-500">
                Запись добавится к существующему долгу с «{selectedExisting.label}».
                Если направления ещё нет — оно будет создано.
              </p>
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
              {isNewCounterparty ? 'Создать' : 'Добавить'}
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
          {debts.map((debt) => {
            const owedToUs = debt.owedToUs ?? (debt.direction === 'OwedToUs' ? debt.balance : 0)
            const weOwe = debt.weOwe ?? (debt.direction === 'WeOwe' ? debt.balance : 0)
            const net = netSignedAmount(debt)
            const isExpanded = expandedDebtId === debt.id

            return (
              <Card key={debt.id} padding="none">
                <button
                  type="button"
                  className="flex w-full items-center justify-between gap-4 px-5 py-4 text-left"
                  onClick={() => {
                    setExpandedDebtId((id) => (id === debt.id ? null : debt.id))
                    setEntryDirection(
                      debt.direction === 'OwedToUs' || debt.direction === 'Mutual'
                        ? 'OwedToUs'
                        : 'WeOwe',
                    )
                    setPayingEntryId(null)
                  }}
                >
                  <div className="space-y-1.5">
                    <p className="font-medium text-slate-900">{debt.counterpartyName}</p>
                    <div className="flex flex-wrap items-center gap-2">
                      <Badge variant={debtStatusVariant(debt)}>{debtStatusLabel(debt)}</Badge>
                      {debt.direction === 'Mutual' && (
                        <span className="text-xs text-slate-500">
                          нам {formatMoney(owedToUs, debt.currency)} · мы{' '}
                          {formatMoney(weOwe, debt.currency)}
                        </span>
                      )}
                    </div>
                  </div>
                  <MoneyDisplay
                    amount={net}
                    currency={debt.currency}
                    signed
                  />
                </button>

                {isExpanded && (
                  <div className="border-t border-slate-100 px-5 py-4 space-y-4">
                    {debt.direction === 'Mutual' && (
                      <div className="grid gap-2 text-sm sm:grid-cols-2">
                        <div className="rounded-xl border border-emerald-100 bg-emerald-50/60 px-3 py-2">
                          <p className="text-xs text-emerald-700">Нам должны</p>
                          <MoneyDisplay amount={owedToUs} currency={debt.currency} size="sm" />
                        </div>
                        <div className="rounded-xl border border-amber-100 bg-amber-50/60 px-3 py-2">
                          <p className="text-xs text-amber-700">Мы должны</p>
                          <MoneyDisplay amount={weOwe} currency={debt.currency} size="sm" />
                        </div>
                      </div>
                    )}

                    {(debt.entries ?? []).length > 0 && (
                      <ul className="divide-y divide-slate-100 rounded-xl border border-slate-200">
                        {(debt.entries ?? []).map((entry) => {
                          const remaining = entryRemaining(entry)
                          const paid = entry.paidAmount ?? (entry.isPaid ? entry.amount : 0)
                          const isPaying = payingEntryId === entry.id

                          return (
                            <li key={entry.id} className="space-y-2 px-4 py-3">
                              <div className="flex items-start justify-between gap-3">
                                <div>
                                  <p className="font-medium text-slate-900">
                                    {entry.description || 'Запись'}
                                  </p>
                                  <p className="text-xs text-slate-500">
                                    {formatDateTime(entry.createdAt)}
                                    {entry.direction && (
                                      <>
                                        {' · '}
                                        {entry.direction === 'OwedToUs'
                                          ? 'нам должны'
                                          : 'мы должны'}
                                      </>
                                    )}
                                  </p>
                                  {paid > 0 && !entry.isPaid && (
                                    <p className="mt-1 text-xs text-slate-500">
                                      Осталось {formatMoney(remaining, entry.currency)} из{' '}
                                      {formatMoney(entry.amount, entry.currency)}
                                    </p>
                                  )}
                                </div>
                                <div className="flex flex-wrap items-center justify-end gap-2">
                                  <MoneyDisplay
                                    amount={entry.isPaid ? entry.amount : remaining}
                                    currency={entry.currency}
                                    size="sm"
                                    className={entry.isPaid ? 'text-slate-400 line-through' : ''}
                                  />
                                  {entry.isPaid ? (
                                    <Button
                                      size="sm"
                                      variant="ghost"
                                      onClick={() => void togglePaid.mutateAsync(entry.id)}
                                    >
                                      Снять
                                    </Button>
                                  ) : (
                                    <Button
                                      size="sm"
                                      variant="ghost"
                                      onClick={() =>
                                        isPaying ? setPayingEntryId(null) : startPayment(entry)
                                      }
                                    >
                                      {isPaying ? 'Отмена' : 'Оплачено'}
                                    </Button>
                                  )}
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
                              </div>

                              {isPaying && (
                                <div className="flex flex-col gap-2 sm:flex-row sm:items-end">
                                  <Input
                                    label="Сумма погашения"
                                    value={payAmount}
                                    onChange={(e) => setPayAmount(e.target.value)}
                                    placeholder={String(remaining)}
                                  />
                                  <Button
                                    loading={payEntry.isPending}
                                    onClick={() => void submitPayment(entry)}
                                  >
                                    Внести
                                  </Button>
                                  {Number.parseFloat(payAmount.replace(',', '.')) < remaining && (
                                    <Button
                                      variant="secondary"
                                      loading={payEntry.isPending}
                                      onClick={() => {
                                        setPayAmount(String(remaining))
                                        void payEntry.mutateAsync({
                                          entryId: entry.id,
                                          amount: remaining,
                                        }).then(() => {
                                          setPayingEntryId(null)
                                          setPayAmount('')
                                        })
                                      }}
                                    >
                                      Всё
                                    </Button>
                                  )}
                                </div>
                              )}
                            </li>
                          )
                        })}
                      </ul>
                    )}

                    <div className="flex flex-col gap-2 sm:flex-row sm:items-end">
                      <Select
                        label="Направление записи"
                        value={entryDirection}
                        onChange={(e) => setEntryDirection(e.target.value as DebtDirection)}
                        options={[
                          { value: 'WeOwe', label: 'Мы должны' },
                          { value: 'OwedToUs', label: 'Нам должны' },
                        ]}
                      />
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
                        loading={addEntry.isPending || createDebt.isPending}
                        onClick={() => void handleAddEntry(debt)}
                      >
                        Добавить запись
                      </Button>
                    </div>

                    <Button
                      variant="ghost"
                      size="sm"
                      className="text-red-600 hover:bg-red-50"
                      loading={deleteDebt.isPending}
                      onClick={() => void deleteDebtGroup(debt)}
                    >
                      Удалить долг
                    </Button>
                  </div>
                )}
              </Card>
            )
          })}
        </div>
      )}
    </div>
  )
}
