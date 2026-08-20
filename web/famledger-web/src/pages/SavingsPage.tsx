import { useMemo, useState, type FormEvent } from 'react'
import {
  useCreateGoal,
  useDeleteGoal,
  useDepositSavings,
  usePermissions,
  useSavings,
  useSettings,
  useWithdrawSavings,
} from '../api/hooks'
import type { SavingsEntry } from '../api/types'
import { currencyOptions } from '../api/types'
import { Card } from '../components/ui/Card'
import { EmptyState, PageHeader, Spinner, Badge } from '../components/ui/Tabs'
import { Button } from '../components/ui/Button'
import { useConfirmDialog } from '../components/ui/ConfirmDialog'
import { Input, Select } from '../components/ui/Input'
import { formatMoney } from '../lib/format'
import { ApiError } from '../api/client'

function formatPeriodLabel(entry: SavingsEntry): string {
  if (entry.periodLabel?.trim()) return entry.periodLabel
  if (entry.periodStart) {
    const date = new Date(entry.periodStart)
    if (!Number.isNaN(date.getTime())) {
      return date.toLocaleDateString('ru-RU', { month: 'long', year: 'numeric' })
    }
  }
  return 'Период'
}

export function SavingsPage() {
  const { canManagePlan } = usePermissions()
  const { confirm } = useConfirmDialog()
  const { data, isLoading, isError, refetch } = useSavings()
  const { data: settings } = useSettings()
  const createGoal = useCreateGoal()
  const deleteGoal = useDeleteGoal()
  const deposit = useDepositSavings()
  const withdraw = useWithdrawSavings()

  const [goalName, setGoalName] = useState('')
  const [goalTarget, setGoalTarget] = useState('')
  const [goalCurrency, setGoalCurrency] = useState('')
  const [movementMode, setMovementMode] = useState<'deposit' | 'withdraw'>('deposit')
  const [depositAmount, setDepositAmount] = useState('')
  const [depositCurrency, setDepositCurrency] = useState('')
  const [movementError, setMovementError] = useState('')

  const plans = Array.isArray(data?.plans) ? data.plans : []
  const goals = Array.isArray(data?.goals) ? data.goals : []
  const currency = settings?.baseCurrency ?? data?.baseCurrency ?? plans[0]?.currency ?? 'RSD'
  const depositCurrencyValue = depositCurrency || currency
  const goalCurrencyValue = goalCurrency || currency

  const totalActual = useMemo(
    () =>
      data?.balance ??
      plans.reduce((sum, e) => sum + (e.actualBaseAmount ?? e.actualAmount ?? 0), 0),
    [data?.balance, plans],
  )

  const periodsWithDeposits = useMemo(
    () =>
      [...plans]
        .filter((p) => (p.actualByCurrency?.length ?? 0) > 0 || (p.actualBaseAmount ?? p.actualAmount) > 0)
        .reverse(),
    [plans],
  )

  async function handleCreateGoal(event: FormEvent) {
    event.preventDefault()
    const target = Number.parseFloat(goalTarget.replace(',', '.'))
    const name = goalName.trim()
    if (!name || Number.isNaN(target) || target <= 0) return
    await createGoal.mutateAsync({
      name,
      targetAmount: target,
      currency: goalCurrencyValue,
    })
    setGoalName('')
    setGoalTarget('')
  }

  async function handleMovement(event: FormEvent) {
    event.preventDefault()
    setMovementError('')
    const amount = Number.parseFloat(depositAmount.replace(',', '.'))
    if (Number.isNaN(amount) || amount <= 0) return
    try {
      if (movementMode === 'deposit') {
        await deposit.mutateAsync({ amount, currency: depositCurrencyValue })
      } else {
        await withdraw.mutateAsync({ amount, currency: depositCurrencyValue })
      }
      setDepositAmount('')
    } catch (err) {
      if (err instanceof ApiError) {
        const body = err.body as { message?: string } | undefined
        setMovementError(body?.message || 'Не удалось выполнить операцию')
      } else {
        setMovementError('Не удалось выполнить операцию')
      }
    }
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
        title="Не удалось загрузить копилку"
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
        title="Копилка"
        subtitle={
          canManagePlan
            ? 'Баланс, цели и факт по периодам'
            : 'Просмотр копилки (изменения доступны главе и помощнику)'
        }
      />

      <div className="rounded-2xl border border-emerald-200 bg-emerald-50 p-5">
        <p className="text-sm text-emerald-700">Баланс копилки</p>
        <p className="mt-1 text-3xl font-bold tabular-nums text-emerald-900">
          {formatMoney(data.balance, currency)}
        </p>
        <p className="mt-1 text-sm text-emerald-700">
          В этом периоде:{' '}
          {formatMoney(
            data.current.actualBaseAmount ?? data.current.actualAmount ?? 0,
            currency,
          )}
        </p>
        <p className="mt-1 text-xs text-emerald-600/80">Итог по текущему курсу</p>
      </div>

      {canManagePlan && (
        <Card>
          <form
            className="flex flex-col gap-3 sm:flex-row sm:items-end"
            onSubmit={(e) => void handleMovement(e)}
          >
            <Select
              label="Действие"
              value={movementMode}
              onChange={(e) => {
                setMovementMode(e.target.value as 'deposit' | 'withdraw')
                setMovementError('')
              }}
              options={[
                { value: 'deposit', label: 'Пополнить копилку' },
                { value: 'withdraw', label: 'Взять из копилки' },
              ]}
            />
            <Input
              label="Сумма"
              value={depositAmount}
              onChange={(e) => setDepositAmount(e.target.value)}
              placeholder="1000"
            />
            <Select
              label="Валюта"
              value={depositCurrencyValue}
              onChange={(e) => setDepositCurrency(e.target.value)}
              options={currencyOptions}
            />
            <Button
              type="submit"
              loading={deposit.isPending || withdraw.isPending}
              className="shrink-0"
            >
              {movementMode === 'deposit' ? 'Внести' : 'Снять'}
            </Button>
          </form>
          {movementError && (
            <p className="mt-2 text-sm text-red-600">{movementError}</p>
          )}
          <p className="mt-2 text-sm text-slate-500">
            {movementMode === 'deposit'
              ? `В списке отобразится в выбранной валюте, итог — в ${currency} по текущему курсу.`
              : `Снятие нельзя больше баланса (${formatMoney(data.balance, currency)}).`}
          </p>
        </Card>
      )}

      <section className="space-y-3">
        <h2 className="text-lg font-semibold text-slate-900">Цели</h2>

        {canManagePlan && (
          <Card>
            <form
              className="flex flex-col gap-3 sm:flex-row sm:items-end"
              onSubmit={(e) => void handleCreateGoal(e)}
            >
              <Input
                label="Название"
                value={goalName}
                onChange={(e) => setGoalName(e.target.value)}
                placeholder="Отпуск"
              />
              <Input
                label="Цель"
                value={goalTarget}
                onChange={(e) => setGoalTarget(e.target.value)}
                placeholder="500"
              />
              <Select
                label="Валюта"
                value={goalCurrencyValue}
                onChange={(e) => setGoalCurrency(e.target.value)}
                options={currencyOptions}
              />
              <Button type="submit" loading={createGoal.isPending} className="shrink-0">
                Добавить цель
              </Button>
            </form>
          </Card>
        )}

        {goals.length === 0 ? (
          <EmptyState
            title="Целей пока нет"
            description={
              canManagePlan
                ? 'Прогресс считается из баланса копилки. При достижении цели семья получит уведомление.'
                : undefined
            }
          />
        ) : (
          <Card padding="none">
            <ul className="divide-y divide-slate-100">
              {goals.map((goal) => {
                const goalCur = goal.currency || currency
                const pct =
                  goal.targetAmount > 0
                    ? Math.min(100, Math.round((goal.progress / goal.targetAmount) * 100))
                    : 0
                return (
                  <li key={goal.id} className="space-y-3 px-5 py-4">
                    <div className="flex items-start justify-between gap-4">
                      <div className="min-w-0">
                        <div className="flex flex-wrap items-center gap-2">
                          <p className="font-medium text-slate-900">{goal.name}</p>
                          {goal.isCompleted && <Badge variant="success">Достигнута</Badge>}
                        </div>
                        <p className="mt-1 text-sm text-slate-500">
                          {formatMoney(goal.progress, goalCur)} /{' '}
                          {formatMoney(goal.targetAmount, goalCur)}
                          <span className="ml-2 tabular-nums text-slate-400">{pct}%</span>
                        </p>
                        <p className="mt-0.5 text-xs text-slate-400">
                          Из баланса копилки по текущему курсу
                        </p>
                      </div>
                      {canManagePlan && (
                        <Button
                          variant="ghost"
                          size="sm"
                          className="shrink-0 text-red-600 hover:bg-red-50"
                          loading={deleteGoal.isPending}
                          onClick={async () => {
                            const accepted = await confirm({
                              title: `Удалить цель «${goal.name}»?`,
                              message: 'Сама копилка не изменится.',
                            })
                            if (accepted) {
                              void deleteGoal.mutateAsync(goal.id)
                            }
                          }}
                        >
                          Удалить
                        </Button>
                      )}
                    </div>

                    <div className="h-2 overflow-hidden rounded-full bg-slate-100">
                      <div
                        className={`h-full rounded-full transition-all ${
                          goal.isCompleted ? 'bg-emerald-500' : 'bg-brand-500'
                        }`}
                        style={{ width: `${pct}%` }}
                      />
                    </div>
                  </li>
                )
              })}
            </ul>
          </Card>
        )}
      </section>

      {periodsWithDeposits.length > 0 && (
        <section className="space-y-3">
          <h2 className="text-lg font-semibold text-slate-900">Факт по периодам</h2>
          <Card padding="none">
            <ul className="divide-y divide-slate-100">
              {periodsWithDeposits.map((entry) => {
                const lines =
                  entry.actualByCurrency && entry.actualByCurrency.length > 0
                    ? entry.actualByCurrency
                    : [
                        {
                          amount: entry.actualBaseAmount ?? entry.actualAmount,
                          currency: entry.currency || currency,
                        },
                      ]
                return (
                  <li key={entry.id} className="space-y-1 px-5 py-3">
                    <p className="text-sm font-medium text-slate-700">
                      {formatPeriodLabel(entry)}
                    </p>
                    {lines.map((line) => (
                      <p
                        key={`${entry.id}-${line.currency}`}
                        className="tabular-nums font-semibold text-slate-900"
                      >
                        {formatMoney(line.amount, line.currency)}
                      </p>
                    ))}
                  </li>
                )
              })}
            </ul>
            <div className="flex items-center justify-between border-t border-slate-200 bg-slate-50 px-5 py-3">
              <p className="text-sm font-medium text-slate-600">Итого</p>
              <p className="tabular-nums font-bold text-slate-900">
                {formatMoney(totalActual, currency)}
              </p>
            </div>
          </Card>
        </section>
      )}
    </div>
  )
}
