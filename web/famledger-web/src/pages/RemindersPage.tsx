import { useEffect, useMemo, useRef, useState, type FormEvent } from 'react'
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
import { ReminderScheduleDialog } from '../components/ReminderScheduleDialog'
import { Input, Select } from '../components/ui/Input'
import {
  ThresholdEditor,
  normalizeThresholds,
} from '../components/ThresholdEditor'
import {
  draftToApi,
  emptyScheduleDraft,
  formatScheduleSummary,
  scheduleFromApi,
  type ScheduleDraft,
} from '../lib/reminderTime'

const STANDARD_META: Record<
  Exclude<ReminderKind, 'Custom'>,
  { title: string; description: string; needsTime: boolean; needsThreshold: boolean }
> = {
  DailyBalance: {
    title: 'Сводка по балансу',
    description:
      'В выбранные дни и время — остаток, дневной бюджет и статистика. Не придёт, если за час до этого было любое действие в приложении или боте.',
    needsTime: true,
    needsThreshold: false,
  },
  BudgetAlert: {
    title: 'Лимит бюджета',
    description:
      'При добавлении расхода — если «Доступно сегодня» достигло одного из порогов от дневного бюджета или ушло в минус. Каждый порог — отдельное уведомление; после удаления расхода порог может сработать снова. В Telegram — сообщение, на сайте — всплывашка.',
    needsTime: false,
    needsThreshold: true,
  },
  EveningCheckIn: {
    title: 'Вечерний чек-ин',
    description:
      'В выбранные дни напоминает записать расходы. Не придёт, если за час до времени уже что-то записывали.',
    needsTime: true,
    needsThreshold: false,
  },
  PeriodEnding: {
    title: 'Конец периода',
    description: 'В выбранные дни, когда до конца периода осталось не больше 3 дней.',
    needsTime: true,
    needsThreshold: false,
  },
  UnpaidDebts: {
    title: 'Незакрытые долги',
    description: 'В выбранные дни — список открытых долгов, не чаще раза в неделю.',
    needsTime: true,
    needsThreshold: false,
  },
  UnpaidPlanned: {
    title: 'Неоплаченные плановые',
    description:
      'В выбранные дни, если день списания постоянного расхода уже наступил, а он не отмечен оплаченным.',
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
  const [scheduleDraft, setScheduleDraft] = useState<ScheduleDraft>(emptyScheduleDraft)
  const [audience, setAudience] = useState<ReminderAudience>('Self')
  const [isEnabled, setIsEnabled] = useState(true)
  const [scheduleTarget, setScheduleTarget] = useState<Reminder | null>(null)
  const [draftScheduleOpen, setDraftScheduleOpen] = useState(false)
  const formRef = useRef<HTMLDivElement>(null)

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
    const alreadyCreating = showForm && editing === null
    setEditing(null)
    setMessage('')
    setScheduleDraft(emptyScheduleDraft())
    setAudience('Self')
    setIsEnabled(true)
    setShowForm(true)
    if (alreadyCreating) scrollFormIntoView()
  }

  function scrollFormIntoView() {
    const form = formRef.current
    if (!form) return
    form.scrollIntoView({ behavior: 'smooth', block: 'start' })
    form.querySelector<HTMLInputElement>('input:not([type="checkbox"])')?.focus({ preventScroll: true })
  }

  function openEditCustom(reminder: Reminder) {
    setEditing(reminder)
    setMessage(reminder.message ?? '')
    setScheduleDraft(scheduleFromApi(reminder.schedule, reminder.timeUtc))
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

  useEffect(() => {
    if (!showForm) return
    scrollFormIntoView()
  }, [showForm, editing?.id])

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    const trimmed = message.trim()
    if (!trimmed) return
    if (!scheduleDraft.days.some((day) => day.enabled)) return

    const schedule = draftToApi(scheduleDraft)
    const nextAudience: ReminderAudience =
      audience === 'Family' && !isPersonal ? 'Family' : 'Self'

    if (editing) {
      await updateReminder.mutateAsync({
        id: editing.id,
        message: trimmed,
        timeUtc: schedule.days[0]?.timeUtc,
        audience: nextAudience,
        isEnabled,
        schedule,
      })
    } else {
      await createReminder.mutateAsync({
        message: trimmed,
        timeUtc: schedule.days[0]?.timeUtc,
        audience: nextAudience,
        schedule,
      })
    }
    closeForm()
  }

  async function saveReminderSchedule(reminder: Reminder, draft: ScheduleDraft) {
    const schedule = draftToApi(draft)
    await updateReminder.mutateAsync({
      id: reminder.id,
      message: reminder.message,
      timeUtc: schedule.days[0]?.timeUtc ?? reminder.timeUtc,
      audience: reminder.audience,
      isEnabled: reminder.isEnabled,
      thresholdPercents: reminder.thresholdPercents,
      schedule,
    })
    setScheduleTarget(null)
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
        subtitle="Стандартные уведомления и свои тексты в Telegram. Лимиты по категориям — в Настройках → Категории и лимиты."
        action={<Button onClick={openCreate}>Своё напоминание</Button>}
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
                      <div className="flex min-w-0 flex-col gap-2 sm:col-span-2 sm:flex-row sm:items-center sm:justify-between">
                        <p className="text-sm text-slate-700">
                          {formatScheduleSummary(reminder.schedule, reminder.timeUtc)}
                        </p>
                        <Button
                          type="button"
                          variant="secondary"
                          size="sm"
                          onClick={() => setScheduleTarget(reminder)}
                        >
                          Расписание
                        </Button>
                      </div>
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

      {showForm && (
        <div ref={formRef} className="scroll-mt-20">
          <Card>
          <form className="space-y-4" onSubmit={(e) => void handleSubmit(e)}>
            <Input
              label="Текст"
              value={message}
              onChange={(e) => setMessage(e.target.value)}
              placeholder="Не забудь записать расходы"
              required
            />
            <div className="flex min-w-0 flex-col gap-3 rounded-2xl border border-slate-200 bg-slate-50 px-4 py-3 sm:flex-row sm:items-center sm:justify-between">
              <div className="min-w-0">
                <p className="text-sm font-medium text-slate-700">Расписание</p>
                <p className="mt-1 text-sm text-slate-600">
                  {formatScheduleSummary(draftToApi(scheduleDraft))}
                </p>
              </div>
              <Button type="button" variant="secondary" onClick={() => setDraftScheduleOpen(true)}>
                Настроить
              </Button>
            </div>
            <div className="grid gap-4 sm:grid-cols-2">
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
        </div>
      )}

      <div className="space-y-3">
        <h2 className="text-sm font-semibold text-slate-900">Свои напоминания</h2>
        {!custom.length ? (
          <EmptyState
            title="Своих напоминаний пока нет"
            description="Можно добавить свой текст в Telegram в выбранные дни и время."
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
                      {formatScheduleSummary(reminder.schedule, reminder.timeUtc)} ·{' '}
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
                        onClick={() => setScheduleTarget(reminder)}
                      >
                        Расписание
                      </Button>
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

      {scheduleTarget && (
        <ReminderScheduleDialog
          initial={scheduleFromApi(scheduleTarget.schedule, scheduleTarget.timeUtc)}
          saving={updateReminder.isPending}
          onCancel={() => setScheduleTarget(null)}
          onSave={(draft) => void saveReminderSchedule(scheduleTarget, draft)}
        />
      )}

      {draftScheduleOpen && (
        <ReminderScheduleDialog
          initial={scheduleDraft}
          onCancel={() => setDraftScheduleOpen(false)}
          onSave={(draft) => {
            setScheduleDraft(draft)
            setDraftScheduleOpen(false)
          }}
        />
      )}
    </div>
  )
}
