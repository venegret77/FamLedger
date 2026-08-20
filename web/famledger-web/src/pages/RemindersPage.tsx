import { useState, type FormEvent } from 'react'
import {
  useCreateReminder,
  useDeleteReminder,
  useReminders,
  useSettings,
  useUpdateReminder,
} from '../api/hooks'
import type { Reminder, ReminderAudience } from '../api/types'
import { Card } from '../components/ui/Card'
import { EmptyState, PageHeader, Spinner, Badge } from '../components/ui/Tabs'
import { Button } from '../components/ui/Button'
import { useConfirmDialog } from '../components/ui/ConfirmDialog'
import { Input, Select } from '../components/ui/Input'
import {
  currentLocalTimeHm,
  localTimeToUtc,
  utcTimeToLocal,
} from '../lib/reminderTime'

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

  function openCreate() {
    setEditing(null)
    setMessage('')
    setTimeLocal(currentLocalTimeHm())
    setAudience('Self')
    setIsEnabled(true)
    setShowForm(true)
  }

  function openEdit(reminder: Reminder) {
    setEditing(reminder)
    setMessage(reminder.message)
    setTimeLocal(utcTimeToLocal(reminder.timeUtc))
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
        subtitle="Ежедневные сообщения в Telegram по вашему локальному времени"
        action={
          !showForm ? (
            <Button onClick={openCreate}>Добавить</Button>
          ) : undefined
        }
      />

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

      {!reminders?.length ? (
        <EmptyState
          title="Напоминаний пока нет"
          description="Создайте ежедневное напоминание — бот пришлёт текст в Telegram."
          action={!showForm ? <Button onClick={openCreate}>Добавить</Button> : undefined}
        />
      ) : (
        <Card padding="none">
          <ul className="divide-y divide-slate-100">
            {reminders.map((reminder) => (
              <li
                key={reminder.id}
                className="flex flex-col gap-3 px-5 py-4 sm:flex-row sm:items-center sm:justify-between"
              >
                <div className="min-w-0 space-y-1">
                  <p className="font-medium text-slate-900">{reminder.message}</p>
                  <p className="text-sm text-slate-500">
                    {utcTimeToLocal(reminder.timeUtc)} ·{' '}
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
                      onClick={() => openEdit(reminder)}
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
  )
}
