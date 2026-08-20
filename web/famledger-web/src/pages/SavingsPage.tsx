import { useState, type FormEvent } from 'react'
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
import { Card } from '../components/ui/Card'
import { EmptyState, PageHeader, Spinner, Tabs } from '../components/ui/Tabs'
import { Button } from '../components/ui/Button'
import { useConfirmDialog } from '../components/ui/ConfirmDialog'
import { Input } from '../components/ui/Input'
import { formatMoney } from '../lib/format'

const savingsTabs = [
  { id: 'current', label: 'Текущий' },
  { id: 'plans', label: 'Планы' },
]

export function SavingsPage() {
  const [activeTab, setActiveTab] = useState('current')
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
        title="Не удалось загрузить накопления"
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
      <PageHeader title="Накопления" subtitle="Копилка, цели и планы по месяцам" />

      <Tabs tabs={savingsTabs} activeTab={activeTab} onChange={setActiveTab} />

      {activeTab === 'current' && (
        <>
          <div className="grid gap-3 sm:grid-cols-2">
            <div className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
              <p className="text-sm text-slate-500">План (текущий период)</p>
              <p className="mt-1 text-2xl font-bold text-slate-900">
                {formatMoney(data.current.plannedAmount, currency)}
              </p>
            </div>
            <div className="rounded-2xl border border-emerald-200 bg-emerald-50 p-5">
              <p className="text-sm text-emerald-700">Отложено всего</p>
              <p className="mt-1 text-2xl font-bold text-emerald-800">
                {formatMoney(data.balance, currency)}
              </p>
              <p className="mt-1 text-sm text-emerald-700">
                В этом периоде: {formatMoney(data.current.actualAmount, currency)}
              </p>
            </div>
          </div>

          {canManagePlan && (
            <div className="grid gap-3 lg:grid-cols-2">
              <Card>
                <form className="flex flex-col gap-3 sm:flex-row sm:items-end" onSubmit={(e) => void handleDeposit(e)}>
                  <Input
                    label="Пополнить копилку"
                    value={depositAmount}
                    onChange={(e) => setDepositAmount(e.target.value)}
                    placeholder="100"
                  />
                  <Button type="submit" loading={deposit.isPending} className="shrink-0">
                    Внести
                  </Button>
                </form>
              </Card>
              <Card>
                <form className="flex flex-col gap-3 sm:flex-row sm:items-end" onSubmit={(e) => void handleSetPlan(e)}>
                  <Input
                    label="План на текущий период"
                    value={planAmount}
                    onChange={(e) => setPlanAmount(e.target.value)}
                    placeholder={String(data.current.plannedAmount)}
                  />
                  <Button type="submit" loading={setPlan.isPending} className="shrink-0">
                    Сохранить план
                  </Button>
                </form>
              </Card>
            </div>
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
                    label="Сумма"
                    value={goalTarget}
                    onChange={(e) => setGoalTarget(e.target.value)}
                    placeholder="500"
                  />
                  <Button type="submit" loading={createGoal.isPending} className="shrink-0">
                    Добавить цель
                  </Button>
                </form>
              </Card>
            )}

            {goals.length === 0 ? (
              <p className="text-sm text-slate-500">Целей пока нет.</p>
            ) : (
              <Card padding="none">
                <ul className="divide-y divide-slate-100">
                  {goals.map((goal) => (
                    <li key={goal.id} className="px-5 py-4 space-y-3">
                      <div className="flex items-center justify-between gap-4">
                        <div>
                          <p className="font-medium text-slate-900">{goal.name}</p>
                          <p className="text-sm text-slate-500">
                            {formatMoney(goal.progress, currency)} /{' '}
                            {formatMoney(goal.targetAmount, currency)}
                          </p>
                        </div>
                        <div className="flex items-center gap-2">
                          {goal.isCompleted && (
                            <span className="text-sm font-medium text-emerald-600">Достигнута</span>
                          )}
                          {canManagePlan && (
                            <Button
                              variant="ghost"
                              size="sm"
                              className="text-red-600 hover:bg-red-50"
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
                            placeholder="50"
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
                  ))}
                </ul>
              </Card>
            )}
          </section>
        </>
      )}

      {activeTab === 'plans' && (
        <>
          {plans.length === 0 ? (
            <EmptyState
              title="Планов пока нет"
              description="Задайте план на текущий период во вкладке «Текущий»."
            />
          ) : (
            <Card padding="none">
              <ul className="divide-y divide-slate-100">
                {plans.map((entry) => (
                  <li key={entry.id} className="flex items-center justify-between gap-4 px-5 py-4">
                    <div>
                      <p className="font-medium text-slate-900">
                        {entry.periodLabel ?? 'Период'}
                      </p>
                      {entry.periodStart && entry.periodEnd && (
                        <p className="text-sm text-slate-500">
                          {entry.periodStart} — {entry.periodEnd}
                        </p>
                      )}
                    </div>
                    <div className="text-right">
                      <p className="text-sm text-slate-500">План / факт</p>
                      <p className="font-medium text-slate-900">
                        {formatMoney(entry.plannedAmount, entry.currency)} /{' '}
                        {formatMoney(entry.actualAmount, entry.currency)}
                      </p>
                    </div>
                  </li>
                ))}
              </ul>
            </Card>
          )}
        </>
      )}
    </div>
  )
}
