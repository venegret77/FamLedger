import { useMemo, useState, type FormEvent } from 'react'
import {
  useCreateReminder,
  useDeleteReminder,
  useReminders,
  useSettings,
  useUpdateReminder,
} from '../api/hooks'
import type { Reminder, ReminderAudience, ReminderKind } from '../api/types'
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
    description: 'Ежедневно в заданное время — остаток, дневной бюджет и статистика.',
    needsTime: true,
    needsThreshold: false,
  },
  BudgetAlert: {
    title: 'Лимит бюджета',
    description: 'Когда потрачено от порога (например 80%) или вышли за рамки.',
    needsTime: false,
    needsThreshold: true,
  },
  EveningCheckIn: {
    title: 'Вечерний чек-ин',
    description: 'Напоминание записать расходы за день.',
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
}

export function RemindersPage() {
  const { confirm } = useConfirmDialog()
  const { data: settings } = useSettings()
  const { data: reminders, isLoading, isError, refetch } = useReminders()
  const createReminder = useCreateReminder()
  const updateReminder = useUpdateReminder()
  const deleteReminder = useDeleteReminder()

  const [editing, setEditing] = useState<Reminder | null>(null)
  const [showForm, setShowForm] = useState(false)
  const [message, setMessage] = useState('')
  const [timeLocal, setTimeLocal] = useState(currentLocalTimeHm)
  const [audience, setAudience] = useState<ReminderAudience>('Self')
  const [isEnabled, setIsEnabled] = useState(true)

  const isPersonal = settings?.isPersonal ?? true
  const audienceOptions = [
    { value: 'Self', label: 'Только я' },
    ...(!isPersonal ? [{ value: 'Family', label: 'Вся семья' }] : []),
  ]

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

  async function toggleStandard(reminder: Reminder, enabled: boolean) {
    await updateReminder.mutateAsync({
      id: reminder.id,
      message: reminder.message,
      timeUtc: reminder.timeUtc,
      audience: reminder.audience,
      isEnabled: enabled,
      thresholdPercent: reminder.thresholdPercent,
    })
  }

  async function saveStandardTime(reminder: Reminder, localHm: string) {
    await updateReminder.mutateAsync({
      id: reminder.id,
      message: reminder.message,
      timeUtc: localTimeToUtc(localHm),
      audience: reminder.audience,
      isEnabled: reminder.isEnabled,
      thresholdPercent: reminder.thresholdPercent,
    })
  }

  async function saveThreshold(reminder: Reminder, value: number) {
    await updateReminder.mutateAsync({
      id: reminder.id,
      message: reminder.message,
      timeUtc: reminder.timeUtc,
      audience: reminder.audience,
      isEnabled: reminder.isEnabled,
      thresholdPercent: value,
    })
  }

  async function saveAudience(reminder: Reminder, next: ReminderAudience) {
    await updateReminder.mutateAsync({
      id: reminder.id,
      message: reminder.message,
      timeUtc: reminder.timeUtc,
      audience: next,
      isEnabled: reminder.isEnabled,
      thresholdPercent: reminder.thresholdPercent,
    })
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
        title="Не удалось загрузить напоминания"
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
        title="Напоминания"
        subtitle="Стандартные уведомления и свои тексты в Telegram"
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
                  <div className="grid gap-3 sm:grid-cols-3">
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
                      <Input
                        label="Порог %"
                        type="number"
                        min={1}
                        max={100}
                        value={String(reminder.thresholdPercent ?? 80)}
                        onChange={(e) => {
                          const n = Number.parseInt(e.target.value, 10)
                          if (!Number.isNaN(n) && n >= 1 && n <= 100) {
                            void saveThreshold(reminder, n)
                          }
                        }}
                      />
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
