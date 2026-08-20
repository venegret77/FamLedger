import { useMemo, useState, type FormEvent } from 'react'
import {
  useContributeGoal,
  useCreateGoal,
  useDeleteGoal,
  useDepositSavings,
  usePermissions,
  useSavings,
  useSetSavingsPlan,
  useSettings,
} from '../api/hooks'
import type { SavingsEntry } from '../api/types'
import { Card } from '../components/ui/Card'
import { EmptyState, PageHeader, Spinner, Tabs, Badge } from '../components/ui/Tabs'
import { Button } from '../components/ui/Button'
import { useConfirmDialog } from '../components/ui/ConfirmDialog'
import { Input } from '../components/ui/Input'
import { formatMoney } from '../lib/format'

const savingsTabs = [
  { id: 'actual', label: 'Фактическое' },
  { id: 'planned', label: 'Плановое' },
]

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
  const [activeTab, setActiveTab] = useState('actual')
  const { canManagePlan } = usePermissions()
  const { confirm } = useConfirmDialog()
  const { data, isLoading, isError, refetch } = useSavings()
  const { data: settings } = useSettings()
  const createGoal = useCreateGoal()
  const deleteGoal = useDeleteGoal()
  const deposit = useDepositSavings()
  const setPlan = useSetSavingsPlan()
  const contribute = useContributeGoal()

  const [goalName, setGoalName] = useState('')
  const [goalTarget, setGoalTarget] = useState('')
  const [depositAmount, setDepositAmount] = useState('')
  const [planAmount, setPlanAmount] = useState('')
  const [contributeGoalId, setContributeGoalId] = useState('')
  const [contributeAmount, setContributeAmount] = useState('')

  const plans = Array.isArray(data?.plans) ? data.plans : []
  const goals = Array.isArray(data?.goals) ? data.goals : []
  const currency = settings?.baseCurrency ?? plans[0]?.currency ?? 'RSD'

  const totals = useMemo(
    () =>
      plans.reduce(
        (acc, entry) => ({
          actual: acc.actual + (entry.actualAmount ?? 0),
          planned: acc.planned + (entry.plannedAmount ?? 0),
        }),
        { actual: 0, planned: 0 },
      ),
    [plans],
  )

  async function handleCreateGoal(event: FormEvent) {
    event.preventDefault()
    const target = Number.parseFloat(goalTarget.replace(',', '.'))
    const name = goalName.trim()
    if (!name || Number.isNaN(target) || target <= 0) return
    await createGoal.mutateAsync({ name, targetAmount: target })
    setGoalName('')
    setGoalTarget('')
  }

  async function handleDeposit(event: FormEvent) {
    event.preventDefault()
    const amount = Number.parseFloat(depositAmount.replace(',', '.'))
    if (Number.isNaN(amount) || amount <= 0) return
    await deposit.mutateAsync(amount)
    setDepositAmount('')
  }

  async function handleSetPlan(event: FormEvent) {
    event.preventDefault()
    const amount = Number.parseFloat(planAmount.replace(',', '.'))
    if (Number.isNaN(amount) || amount < 0) return
    await setPlan.mutateAsync(amount)
    setPlanAmount('')
  }

  async function handleContributeToGoal(goalId: string) {
    const amount = Number.parseFloat(contributeAmount.replace(',', '.'))
    if (Number.isNaN(amount) || amount <= 0) return
    await contribute.mutateAsync({ goalId, amount })
    setContributeAmount('')
    setContributeGoalId('')
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
            ? 'Факт, цели и план накоплений по периодам'
            : 'Просмотр копилки (изменения доступны главе и помощнику)'
        }
      />

      <Tabs tabs={savingsTabs} activeTab={activeTab} onChange={setActiveTab} />

      {activeTab === 'actual' && (
        <>
          <div className="rounded-2xl border border-emerald-200 bg-emerald-50 p-5">
            <p className="text-sm text-emerald-700">Баланс копилки</p>
            <p className="mt-1 text-3xl font-bold tabular-nums text-emerald-900">
              {formatMoney(data.balance, currency)}
            </p>
            <p className="mt-1 text-sm text-emerald-700">
              В этом периоде: {formatMoney(data.current.actualAmount, currency)}
            </p>
          </div>

          {canManagePlan && (
            <Card>
              <form
                className="flex flex-col gap-3 sm:flex-row sm:items-end"
                onSubmit={(e) => void handleDeposit(e)}
              >
                <Input
                  label="Пополнить копилку"
                  value={depositAmount}
                  onChange={(e) => setDepositAmount(e.target.value)}
                  placeholder="1000"
                />
                <Button type="submit" loading={deposit.isPending} className="shrink-0">
                  Внести
                </Button>
              </form>
            </Card>
          )}

          <section className="space-y-3">
            <div className="flex items-center justify-between gap-3">
              <h2 className="text-lg font-semibold text-slate-900">Цели</h2>
            </div>

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
                    label={`Цель, ${currency}`}
                    value={goalTarget}
                    onChange={(e) => setGoalTarget(e.target.value)}
                    placeholder="50000"
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
                    ? 'Задайте цель — при достижении суммы семья получит уведомление.'
                    : undefined
                }
              />
            ) : (
              <Card padding="none">
                <ul className="divide-y divide-slate-100">
                  {goals.map((goal) => {
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
                              {formatMoney(goal.progress, currency)} /{' '}
                              {formatMoney(goal.targetAmount, currency)}
                              <span className="ml-2 tabular-nums text-slate-400">{pct}%</span>
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
                                  message: 'Прогресс по цели тоже будет удалён.',
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

                        {canManagePlan && !goal.isCompleted && (
                          <div className="flex flex-col gap-2 sm:flex-row sm:items-end">
                            <Input
                              label="Взнос в цель"
                              value={contributeGoalId === goal.id ? contributeAmount : ''}
                              onFocus={() => setContributeGoalId(goal.id)}
                              onChange={(e) => {
                                setContributeGoalId(goal.id)
                                setContributeAmount(e.target.value)
                              }}
                              placeholder="1000"
                            />
                            <Button
                              size="sm"
                              loading={contribute.isPending && contributeGoalId === goal.id}
                              onClick={() => void handleContributeToGoal(goal.id)}
                            >
                              Внести
                            </Button>
                          </div>
                        )}
                      </li>
                    )
                  })}
                </ul>
              </Card>
            )}
          </section>

          {plans.some((p) => p.actualAmount > 0) && (
            <section className="space-y-3">
              <h2 className="text-lg font-semibold text-slate-900">Факт по периодам</h2>
              <Card padding="none">
                <ul className="divide-y divide-slate-100">
                  {[...plans]
                    .filter((p) => p.actualAmount > 0)
                    .reverse()
                    .map((entry) => (
                      <li
                        key={entry.id}
                        className="flex items-center justify-between gap-4 px-5 py-3"
                      >
                        <p className="text-sm font-medium text-slate-700">
                          {formatPeriodLabel(entry)}
                        </p>
                        <p className="tabular-nums font-semibold text-slate-900">
                          {formatMoney(entry.actualAmount, entry.currency || currency)}
                        </p>
                      </li>
                    ))}
                </ul>
                <div className="flex items-center justify-between border-t border-slate-200 bg-slate-50 px-5 py-3">
                  <p className="text-sm font-medium text-slate-600">Итого</p>
                  <p className="tabular-nums font-bold text-slate-900">
                    {formatMoney(totals.actual, currency)}
                  </p>
                </div>
              </Card>
            </section>
          )}
        </>
      )}

      {activeTab === 'planned' && (
        <>
          {canManagePlan && (
            <Card>
              <form
                className="flex flex-col gap-3 sm:flex-row sm:items-end"
                onSubmit={(e) => void handleSetPlan(e)}
              >
                <Input
                  label={`План на текущий период, ${currency}`}
                  value={planAmount}
                  onChange={(e) => setPlanAmount(e.target.value)}
                  placeholder={String(data.current.plannedAmount || '')}
                />
                <Button type="submit" loading={setPlan.isPending} className="shrink-0">
                  Сохранить план
                </Button>
              </form>
              <p className="mt-2 text-sm text-slate-500">
                Сейчас в плане: {formatMoney(data.current.plannedAmount, currency)}
              </p>
            </Card>
          )}

          {plans.length === 0 ? (
            <EmptyState
              title="Планов пока нет"
              description={
                canManagePlan
                  ? 'Задайте план на текущий период выше.'
                  : 'Планы появятся, когда глава или помощник зададут сумму.'
              }
            />
          ) : (
            <Card padding="none">
              <div className="grid grid-cols-[1fr_1fr_1.2fr] gap-2 border-b border-slate-200 bg-slate-50 px-4 py-3 text-xs font-semibold uppercase tracking-wide text-slate-500 sm:px-5">
                <span>Факт</span>
                <span>План</span>
                <span className="text-right sm:text-left">Месяц</span>
              </div>
              <ul className="divide-y divide-slate-100">
                {plans.map((entry) => (
                  <li
                    key={entry.id}
                    className="grid grid-cols-[1fr_1fr_1.2fr] items-center gap-2 px-4 py-3 sm:px-5"
                  >
                    <p className="tabular-nums text-sm font-medium text-slate-900">
                      {formatMoney(entry.actualAmount, entry.currency || currency)}
                    </p>
                    <p className="tabular-nums text-sm font-medium text-slate-900">
                      {formatMoney(entry.plannedAmount, entry.currency || currency)}
                    </p>
                    <p className="text-right text-sm text-slate-600 sm:text-left">
                      {formatPeriodLabel(entry)}
                    </p>
                  </li>
                ))}
              </ul>
              <div className="grid grid-cols-[1fr_1fr_1.2fr] items-center gap-2 border-t border-slate-200 bg-emerald-50/60 px-4 py-3 sm:px-5">
                <p className="tabular-nums text-sm font-bold text-slate-900">
                  {formatMoney(totals.actual, currency)}
                </p>
                <p className="tabular-nums text-sm font-bold text-slate-900">
                  {formatMoney(totals.planned, currency)}
                </p>
                <p className="text-right text-sm font-medium text-slate-600 sm:text-left">
                  Итого
                </p>
              </div>
            </Card>
          )}
        </>
      )}
    </div>
  )
}
