import { useMemo, useState, type FormEvent } from 'react'
import {
  useCategoryLimits,
  useCreateCategoryLimit,
  useCreateReminder,
  useDeleteCategoryLimit,
  useDeleteReminder,
  useReminders,
  useSettings,
  useUpdateCategoryLimit,
  useUpdateReminder,
} from '../api/hooks'
import type {
  CategorySpendingLimit,
  Reminder,
  ReminderAudience,
  ReminderKind,
} from '../api/types'
import { Card, CardTitle } from '../components/ui/Card'
import { EmptyState, PageHeader, Spinner, Badge } from '../components/ui/Tabs'
import { Button } from '../components/ui/Button'
import { useConfirmDialog } from '../components/ui/ConfirmDialog'
import { Input, Select } from '../components/ui/Input'
import {
  currentLocalTimeHm,
  localTimeToUtc,
  utcTimeToLocal,
} from '../lib/reminderTime'

const STANDARD_META: Record<
  Exclude<ReminderKind, 'Custom'>,
  { title: string; description: string; needsTime: boolean; needsThreshold: boolean }
> = {
  DailyBalance: {
    title: 'Сводка по балансу',
    description:
      'Ежедневно в заданное время — остаток, дневной бюджет и статистика. Не придёт, если за час до этого было любое действие в приложении или боте.',
    needsTime: true,
    needsThreshold: false,
  },
  BudgetAlert: {
    title: 'Лимит бюджета',
    description:
      'При добавлении расхода — если «Доступно сегодня» достигло одного из порогов от дневного бюджета или ушло в минус. Каждый порог — отдельное уведомление раз в сутки. В Telegram — сообщение, на сайте — всплывашка.',
    needsTime: false,
    needsThreshold: true,
  },
  EveningCheckIn: {
    title: 'Вечерний чек-ин',
    description:
      'Напоминание записать расходы за день. Не придёт, если за час до времени напоминания уже что-то записывали.',
    needsTime: true,
    needsThreshold: false,
  },
  PeriodEnding: {
    title: 'Конец периода',
    description: 'За 3 дня до конца периода — остаток и сколько дней осталось.',
    needsTime: true,
    needsThreshold: false,
  },
  UnpaidDebts: {
    title: 'Незакрытые долги',
    description: 'Раз в неделю список открытых долгов (если есть).',
    needsTime: true,
    needsThreshold: false,
  },
  UnpaidPlanned: {
    title: 'Неоплаченные плановые',
    description:
      'Если день списания постоянного расхода уже наступил, а он не отмечен оплаченным.',
    needsTime: true,
    needsThreshold: false,
  },
}

function normalizeThresholds(values: number[]): number[] {
  return [...new Set(values.filter((n) => n >= 1 && n <= 100))].sort((a, b) => a - b).slice(0, 10)
}

function ThresholdEditor({
  values,
  onChange,
  disabled,
}: {
  values: number[]
  onChange: (next: number[]) => void
  disabled?: boolean
}) {
  const [draft, setDraft] = useState('')

  function addThreshold() {
    const n = Number.parseInt(draft, 10)
    if (Number.isNaN(n) || n < 1 || n > 100) return
    onChange(normalizeThresholds([...values, n]))
    setDraft('')
  }

  return (
    <div className="space-y-2">
      <p className="text-sm font-medium text-slate-700">Пороги %</p>
      <div className="flex flex-wrap gap-2">
        {values.map((t) => (
          <button
            key={t}
            type="button"
            disabled={disabled}
            onClick={() => onChange(values.filter((x) => x !== t))}
            className="inline-flex items-center gap-1 rounded-md border border-slate-200 bg-slate-50 px-2 py-1 text-sm text-slate-700 hover:bg-slate-100 disabled:opacity-50"
            title="Убрать порог"
          >
            {t}%
            <span aria-hidden className="text-slate-400">
              ×
            </span>
          </button>
        ))}
        {values.length === 0 && (
          <span className="text-sm text-slate-400">Нет порогов</span>
        )}
      </div>
      <div className="flex max-w-xs gap-2">
        <Input
          type="number"
          min={1}
          max={100}
          value={draft}
          placeholder="например 50"
          disabled={disabled || values.length >= 10}
          onChange={(e) => setDraft(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === 'Enter') {
              e.preventDefault()
              addThreshold()
            }
          }}
        />
        <Button
          type="button"
          variant="secondary"
          disabled={disabled || values.length >= 10}
          onClick={addThreshold}
        >
          Добавить
        </Button>
      </div>
    </div>
  )
}

export function RemindersPage() {
  const { confirm } = useConfirmDialog()
  const { data: settings } = useSettings()
  const { data: reminders, isLoading, isError, refetch } = useReminders()
  const {
    data: categoryLimits,
    isLoading: limitsLoading,
  } = useCategoryLimits()
  const createReminder = useCreateReminder()
  const updateReminder = useUpdateReminder()
  const deleteReminder = useDeleteReminder()
  const createLimit = useCreateCategoryLimit()
  const updateLimit = useUpdateCategoryLimit()
  const deleteLimit = useDeleteCategoryLimit()

  const [editing, setEditing] = useState<Reminder | null>(null)
  const [showForm, setShowForm] = useState(false)
  const [message, setMessage] = useState('')
  const [timeLocal, setTimeLocal] = useState(currentLocalTimeHm)
  const [audience, setAudience] = useState<ReminderAudience>('Self')
  const [isEnabled, setIsEnabled] = useState(true)

  const [showLimitForm, setShowLimitForm] = useState(false)
  const [editingLimit, setEditingLimit] = useState<CategorySpendingLimit | null>(null)
  const [limitCategoryId, setLimitCategoryId] = useState('')
  const [limitAmount, setLimitAmount] = useState('')
  const [limitThresholds, setLimitThresholds] = useState<number[]>([50, 80])
  const [limitAudience, setLimitAudience] = useState<ReminderAudience>('Self')
  const [limitEnabled, setLimitEnabled] = useState(true)

  const isPersonal = settings?.isPersonal ?? true
  const audienceOptions = [
    { value: 'Self', label: 'Только я' },
    ...(!isPersonal ? [{ value: 'Family', label: 'Вся семья' }] : []),
  ]

  const expenseCategories = useMemo(
    () =>
      (settings?.categories ?? []).filter(
        (c) => !c.kind || c.kind === 'Expense',
      ),
    [settings?.categories],
  )

  const { standard, custom } = useMemo(() => {
    const list = reminders ?? []
    return {
      standard: list.filter((r) => r.kind !== 'Custom'),
      custom: list.filter((r) => r.kind === 'Custom'),
    }
  }, [reminders])

  function openCreate() {
    setEditing(null)
    setMessage('')
    setTimeLocal(currentLocalTimeHm())
    setAudience('Self')
    setIsEnabled(true)
    setShowForm(true)
  }

  function openEditCustom(reminder: Reminder) {
    setEditing(reminder)
    setMessage(reminder.message ?? '')
    setTimeLocal(reminder.timeUtc ? utcTimeToLocal(reminder.timeUtc) : currentLocalTimeHm())
    setAudience(
      reminder.audience === 'Family' && !isPersonal ? 'Family' : 'Self',
    )
    setIsEnabled(reminder.isEnabled)
    setShowForm(true)
  }

  function closeForm() {
    setShowForm(false)
    setEditing(null)
  }

  function openCreateLimit() {
    setEditingLimit(null)
    setLimitCategoryId(expenseCategories[0]?.id ?? '')
    setLimitAmount('')
    setLimitThresholds([50, 80])
    setLimitAudience('Self')
    setLimitEnabled(true)
    setShowLimitForm(true)
  }

  function openEditLimit(limit: CategorySpendingLimit) {
    setEditingLimit(limit)
    setLimitCategoryId(limit.categoryId)
    setLimitAmount(String(limit.limitAmount))
    setLimitThresholds(normalizeThresholds(limit.thresholdPercents ?? []))
    setLimitAudience(
      limit.audience === 'Family' && !isPersonal ? 'Family' : 'Self',
    )
    setLimitEnabled(limit.isEnabled)
    setShowLimitForm(true)
  }

  function closeLimitForm() {
    setShowLimitForm(false)
    setEditingLimit(null)
  }

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    const trimmed = message.trim()
    if (!trimmed || !timeLocal) return

    const timeUtc = localTimeToUtc(timeLocal)
    const nextAudience: ReminderAudience =
      audience === 'Family' && !isPersonal ? 'Family' : 'Self'

    if (editing) {
      await updateReminder.mutateAsync({
        id: editing.id,
        message: trimmed,
        timeUtc,
        audience: nextAudience,
        isEnabled,
      })
    } else {
      await createReminder.mutateAsync({
        message: trimmed,
        timeUtc,
        audience: nextAudience,
      })
    }
    closeForm()
  }

  async function handleLimitSubmit(event: FormEvent) {
    event.preventDefault()
    const amount = Number.parseFloat(limitAmount.replace(',', '.'))
    if (!limitCategoryId || Number.isNaN(amount) || amount <= 0) return
    const thresholds = normalizeThresholds(limitThresholds)
    if (thresholds.length === 0) return

    const nextAudience: ReminderAudience =
      limitAudience === 'Family' && !isPersonal ? 'Family' : 'Self'

    if (editingLimit) {
      await updateLimit.mutateAsync({
        id: editingLimit.id,
        limitAmount: amount,
        thresholdPercents: thresholds,
        audience: nextAudience,
        isEnabled: limitEnabled,
      })
    } else {
      await createLimit.mutateAsync({
        categoryId: limitCategoryId,
        limitAmount: amount,
        thresholdPercents: thresholds,
        audience: nextAudience,
      })
    }
    closeLimitForm()
  }

  async function toggleStandard(reminder: Reminder, enabled: boolean) {
    await updateReminder.mutateAsync({
      id: reminder.id,
      message: reminder.message,
      timeUtc: reminder.timeUtc,
      audience: reminder.audience,
      isEnabled: enabled,
      thresholdPercents: reminder.thresholdPercents,
    })
  }

  async function saveStandardTime(reminder: Reminder, localHm: string) {
    await updateReminder.mutateAsync({
      id: reminder.id,
      message: reminder.message,
      timeUtc: localTimeToUtc(localHm),
      audience: reminder.audience,
      isEnabled: reminder.isEnabled,
      thresholdPercents: reminder.thresholdPercents,
    })
  }

  async function saveThresholds(reminder: Reminder, values: number[]) {
    const next = normalizeThresholds(values)
    if (next.length === 0) return
    await updateReminder.mutateAsync({
      id: reminder.id,
      message: reminder.message,
      timeUtc: reminder.timeUtc,
      audience: reminder.audience,
      isEnabled: reminder.isEnabled,
      thresholdPercents: next,
    })
  }

  async function saveAudience(reminder: Reminder, next: ReminderAudience) {
    await updateReminder.mutateAsync({
      id: reminder.id,
      message: reminder.message,
      timeUtc: reminder.timeUtc,
      audience: next,
      isEnabled: reminder.isEnabled,
      thresholdPercents: reminder.thresholdPercents,
    })
  }

  if (isLoading || limitsLoading) {
    return (
      <div className="flex justify-center py-20">
        <Spinner />
      </div>
    )
  }

  if (isError) {
    return (
      <EmptyState
        title="Не удалось загрузить напоминания"
        action={
          <Button variant="secondary" onClick={() => void refetch()}>
            Повторить
          </Button>
        }
      />
    )
  }

  const usedCategoryIds = new Set((categoryLimits ?? []).map((l) => l.categoryId))
  const availableCategories = expenseCategories.filter(
    (c) => !usedCategoryIds.has(c.id) || c.id === editingLimit?.categoryId,
  )

  return (
    <div className="space-y-6">
      <PageHeader
        title="Напоминания"
        subtitle="Стандартные уведомления, лимиты категорий и свои тексты в Telegram"
        action={
          !showForm ? (
            <Button onClick={openCreate}>Своё напоминание</Button>
          ) : undefined
        }
      />

      <Card>
        <CardTitle>Стандартные</CardTitle>
        <ul className="mt-4 divide-y divide-slate-100">
          {standard.map((reminder) => {
            const meta = STANDARD_META[reminder.kind as Exclude<ReminderKind, 'Custom'>]
            if (!meta) return null
            return (
              <li key={reminder.id} className="space-y-3 py-4 first:pt-0 last:pb-0">
                <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
                  <div className="min-w-0 space-y-1">
                    <div className="flex flex-wrap items-center gap-2">
                      <p className="font-medium text-slate-900">{meta.title}</p>
                      {!reminder.isEnabled && <Badge>Выкл</Badge>}
                    </div>
                    <p className="text-sm text-slate-500">{meta.description}</p>
                  </div>
                  {reminder.canEdit && (
                    <label className="flex shrink-0 items-center gap-2 text-sm text-slate-700">
                      <input
                        type="checkbox"
                        checked={reminder.isEnabled}
                        onChange={(e) => void toggleStandard(reminder, e.target.checked)}
                        className="size-4 rounded border-slate-300"
                      />
                      Включено
                    </label>
                  )}
                </div>
                {reminder.canEdit && reminder.isEnabled && (
                  <div className="grid gap-3 sm:grid-cols-2">
                    {meta.needsTime && (
                      <Input
                        label="Время (локальное)"
                        type="time"
                        value={
                          reminder.timeUtc
                            ? utcTimeToLocal(reminder.timeUtc)
                            : currentLocalTimeHm()
                        }
                        onChange={(e) => void saveStandardTime(reminder, e.target.value)}
                      />
                    )}
                    {meta.needsThreshold && (
                      <div className="sm:col-span-2">
                        <ThresholdEditor
                          values={normalizeThresholds(reminder.thresholdPercents ?? [80])}
                          onChange={(next) => void saveThresholds(reminder, next)}
                        />
                      </div>
                    )}
                    {!isPersonal && (
                      <Select
                        label="Кому"
                        value={reminder.audience}
                        onChange={(e) =>
                          void saveAudience(
                            reminder,
                            e.target.value as ReminderAudience,
                          )
                        }
                        options={audienceOptions}
                      />
                    )}
                  </div>
                )}
              </li>
            )
          })}
        </ul>
      </Card>

      <Card>
        <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <CardTitle>Лимиты по категориям</CardTitle>
            <p className="mt-1 text-sm text-slate-500">
              Сумма на текущий бюджетный период и пороги %. Уведомление при расходе в этой
              категории.
            </p>
          </div>
          {!showLimitForm && (
            <Button
              variant="secondary"
              onClick={openCreateLimit}
              disabled={availableCategories.length === 0 && !editingLimit}
            >
              Добавить лимит
            </Button>
          )}
        </div>

        {showLimitForm && (
          <form className="mt-4 space-y-4 border-t border-slate-100 pt-4" onSubmit={(e) => void handleLimitSubmit(e)}>
            {!editingLimit && (
              <Select
                label="Категория"
                value={limitCategoryId}
                onChange={(e) => setLimitCategoryId(e.target.value)}
                options={availableCategories.map((c) => ({
                  value: c.id,
                  label: c.name,
                }))}
                required
              />
            )}
            {editingLimit && (
              <p className="text-sm font-medium text-slate-800">{editingLimit.categoryName}</p>
            )}
            <Input
              label={`Лимит (${settings?.baseCurrency ?? 'RSD'})`}
              type="number"
              min={0.01}
              step="0.01"
              value={limitAmount}
              onChange={(e) => setLimitAmount(e.target.value)}
              required
            />
            <ThresholdEditor values={limitThresholds} onChange={setLimitThresholds} />
            {!isPersonal && (
              <Select
                label="Кому"
                value={limitAudience}
                onChange={(e) => setLimitAudience(e.target.value as ReminderAudience)}
                options={audienceOptions}
              />
            )}
            {editingLimit && (
              <label className="flex items-center gap-2 text-sm text-slate-700">
                <input
                  type="checkbox"
                  checked={limitEnabled}
                  onChange={(e) => setLimitEnabled(e.target.checked)}
                  className="size-4 rounded border-slate-300"
                />
                Включено
              </label>
            )}
            <div className="flex flex-wrap gap-2">
              <Button
                type="submit"
                loading={createLimit.isPending || updateLimit.isPending}
              >
                {editingLimit ? 'Сохранить' : 'Создать'}
              </Button>
              <Button type="button" variant="secondary" onClick={closeLimitForm}>
                Отмена
              </Button>
            </div>
          </form>
        )}

        <ul className="mt-4 divide-y divide-slate-100">
          {(categoryLimits ?? []).length === 0 && !showLimitForm ? (
            <li className="py-2">
              <EmptyState
                title="Лимитов пока нет"
                description="Задайте лимит на категорию и пороги, например 50% и 80%."
                action={<Button onClick={openCreateLimit}>Добавить лимит</Button>}
              />
            </li>
          ) : (
            (categoryLimits ?? []).map((limit) => (
              <li
                key={limit.id}
                className="flex flex-col gap-3 py-4 first:pt-0 last:pb-0 sm:flex-row sm:items-center sm:justify-between"
              >
                <div className="min-w-0 space-y-1">
                  <div className="flex flex-wrap items-center gap-2">
                    <p className="font-medium text-slate-900">{limit.categoryName}</p>
                    {!limit.isEnabled && <Badge>Выкл</Badge>}
                    {limit.audience === 'Family' && <Badge>Семья</Badge>}
                  </div>
                  <p className="text-sm text-slate-500">
                    {limit.limitAmount} {settings?.baseCurrency ?? ''} · пороги{' '}
                    {(limit.thresholdPercents ?? []).map((t) => `${t}%`).join(', ') || '—'}
                  </p>
                </div>
                {limit.canEdit && (
                  <div className="flex shrink-0 flex-wrap gap-2">
                    <Button
                      variant="secondary"
                      size="sm"
                      onClick={() => openEditLimit(limit)}
                    >
                      Изменить
                    </Button>
                    <Button
                      variant="ghost"
                      size="sm"
                      className="text-red-600"
                      loading={deleteLimit.isPending}
                      onClick={async () => {
                        const accepted = await confirm({
                          title: 'Удалить лимит?',
                          message: `Лимит для «${limit.categoryName}» будет удалён.`,
                        })
                        if (accepted) void deleteLimit.mutateAsync(limit.id)
                      }}
                    >
                      Удалить
                    </Button>
                  </div>
                )}
              </li>
            ))
          )}
        </ul>
      </Card>

      {showForm && (
        <Card>
          <form className="space-y-4" onSubmit={(e) => void handleSubmit(e)}>
            <Input
              label="Текст"
              value={message}
              onChange={(e) => setMessage(e.target.value)}
              placeholder="Не забудь записать расходы"
              required
            />
            <div className="grid gap-4 sm:grid-cols-2">
              <Input
                label="Время (локальное)"
                type="time"
                value={timeLocal}
                onChange={(e) => setTimeLocal(e.target.value)}
                required
              />
              <Select
                label="Кому"
                value={audience}
                onChange={(e) => setAudience(e.target.value as ReminderAudience)}
                options={audienceOptions}
              />
            </div>
            {editing && (
              <label className="flex items-center gap-2 text-sm text-slate-700">
                <input
                  type="checkbox"
                  checked={isEnabled}
                  onChange={(e) => setIsEnabled(e.target.checked)}
                  className="size-4 rounded border-slate-300"
                />
                Включено
              </label>
            )}
            <div className="flex flex-wrap gap-2">
              <Button
                type="submit"
                loading={createReminder.isPending || updateReminder.isPending}
              >
                {editing ? 'Сохранить' : 'Создать'}
              </Button>
              <Button type="button" variant="secondary" onClick={closeForm}>
                Отмена
              </Button>
            </div>
          </form>
        </Card>
      )}

      <div className="space-y-3">
        <h2 className="text-sm font-semibold text-slate-900">Свои напоминания</h2>
        {!custom.length ? (
          <EmptyState
            title="Своих напоминаний пока нет"
            description="Можно добавить свой ежедневный текст в Telegram."
            action={!showForm ? <Button onClick={openCreate}>Добавить</Button> : undefined}
          />
        ) : (
          <Card padding="none">
            <ul className="divide-y divide-slate-100">
              {custom.map((reminder) => (
                <li
                  key={reminder.id}
                  className="flex flex-col gap-3 px-5 py-4 sm:flex-row sm:items-center sm:justify-between"
                >
                  <div className="min-w-0 space-y-1">
                    <p className="font-medium text-slate-900">{reminder.message}</p>
                    <p className="text-sm text-slate-500">
                      {reminder.timeUtc ? utcTimeToLocal(reminder.timeUtc) : '—'} ·{' '}
                      {reminder.audience === 'Family' ? 'Вся семья' : 'Только я'}
                      {reminder.createdByName ? ` · ${reminder.createdByName}` : ''}
                    </p>
                    <div className="flex flex-wrap gap-2">
                      {!reminder.isEnabled && <Badge>Выкл</Badge>}
                      {reminder.audience === 'Family' && <Badge>Семья</Badge>}
                    </div>
                  </div>
                  {reminder.canEdit && (
                    <div className="flex shrink-0 flex-wrap gap-2">
                      <Button
                        variant="secondary"
                        size="sm"
                        onClick={() => openEditCustom(reminder)}
                      >
                        Изменить
                      </Button>
                      <Button
                        variant="ghost"
                        size="sm"
                        className="text-red-600"
                        loading={deleteReminder.isPending}
                        onClick={async () => {
                          const accepted = await confirm({
                            title: 'Удалить напоминание?',
                            message: 'Ежедневная отправка этого текста прекратится.',
                          })
                          if (accepted) void deleteReminder.mutateAsync(reminder.id)
                        }}
                      >
                        Удалить
                      </Button>
                    </div>
                  )}
                </li>
              ))}
            </ul>
          </Card>
        )}
      </div>
    </div>
  )
}
