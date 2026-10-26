import { useState, type FormEvent } from 'react'
import { Button } from './ui/Button'
import {
  WEEKDAYS,
  type ScheduleDraft,
} from '../lib/reminderTime'

interface ReminderScheduleDialogProps {
  initial: ScheduleDraft
  saving?: boolean
  onCancel: () => void
  onSave: (draft: ScheduleDraft) => void
}

export function ReminderScheduleDialog({
  initial,
  saving = false,
  onCancel,
  onSave,
}: ReminderScheduleDialogProps) {
  const [draft, setDraft] = useState(initial)
  const [error, setError] = useState('')

  function updateDay(day: number, patch: Partial<ScheduleDraft['days'][number]>) {
    setDraft((current) => ({
      ...current,
      days: current.days.map((item) => (item.day === day ? { ...item, ...patch } : item)),
    }))
  }

  function handleSubmit(event: FormEvent) {
    event.preventDefault()
    const enabled = draft.days.filter((day) => day.enabled)
    if (enabled.length === 0) {
      setError('Выберите хотя бы один день')
      return
    }

    const missingTime = draft.sameTimeEveryDay
      ? !draft.sharedTimeLocal
      : enabled.some((day) => !day.timeLocal)
    if (missingTime) {
      setError('Укажите время')
      return
    }

    setError('')
    onSave(draft)
  }

  return (
    <div className="fixed inset-0 z-50 flex items-end justify-center bg-slate-950/50 px-4 py-4 sm:items-center">
      <form
        className="flex max-h-[90dvh] w-full max-w-md flex-col overflow-hidden rounded-3xl bg-white shadow-2xl"
        onSubmit={handleSubmit}
      >
        <div className="space-y-4 overflow-y-auto p-6">
          <div>
            <h2 className="text-lg font-semibold text-slate-900">Когда присылать</h2>
            <p className="mt-1 text-sm text-slate-500">Время местное. Можно выбрать отдельные дни.</p>
          </div>

          <label className="flex items-center gap-2 text-sm text-slate-800">
            <input
              type="checkbox"
              checked={draft.sameTimeEveryDay}
              onChange={(event) =>
                setDraft((current) => ({
                  ...current,
                  sameTimeEveryDay: event.target.checked,
                }))
              }
              className="size-4 rounded border-slate-300"
            />
            Каждый день в одно время
          </label>

          {draft.sameTimeEveryDay && (
            <label className="block space-y-1.5">
              <span className="block text-sm font-medium text-slate-700">Общее время</span>
              <input
                type="time"
                required
                value={draft.sharedTimeLocal}
                onChange={(event) =>
                  setDraft((current) => ({ ...current, sharedTimeLocal: event.target.value }))
                }
                className="w-full min-w-0 rounded-xl border border-slate-200 bg-white px-3.5 py-2.5 text-base text-slate-900 shadow-sm focus:border-brand-500 focus:outline-none focus:ring-2 focus:ring-brand-500/20"
              />
            </label>
          )}

          <ul className="space-y-2">
            {WEEKDAYS.map((weekday) => {
              const day = draft.days.find((item) => item.day === weekday.day)
              if (!day) return null
              const timeValue = draft.sameTimeEveryDay ? draft.sharedTimeLocal : day.timeLocal
              const timeDisabled = draft.sameTimeEveryDay || !day.enabled
              return (
                <li
                  key={weekday.day}
                  className="grid grid-cols-[minmax(0,1fr)_8.25rem] items-center gap-2"
                >
                  <label className="flex min-w-0 items-center gap-2 text-sm text-slate-800">
                    <input
                      type="checkbox"
                      checked={day.enabled}
                      onChange={(event) => updateDay(weekday.day, { enabled: event.target.checked })}
                      className="size-4 shrink-0 rounded border-slate-300"
                    />
                    <span className="truncate">{weekday.label}</span>
                  </label>
                  <input
                    type="time"
                    value={timeValue}
                    disabled={timeDisabled}
                    onChange={(event) => updateDay(weekday.day, { timeLocal: event.target.value })}
                    className="w-full min-w-0 rounded-xl border border-slate-200 bg-white px-2 py-2 text-base text-slate-900 shadow-sm focus:border-brand-500 focus:outline-none focus:ring-2 focus:ring-brand-500/20 disabled:cursor-not-allowed disabled:bg-slate-50 disabled:text-slate-400"
                  />
                </li>
              )
            })}
          </ul>

          {error && <p className="text-sm text-red-600">{error}</p>}
        </div>

        <div className="flex justify-end gap-3 border-t border-slate-100 px-6 py-4">
          <Button type="button" variant="secondary" onClick={onCancel} disabled={saving}>
            Отмена
          </Button>
          <Button type="submit" loading={saving}>
            Сохранить
          </Button>
        </div>
      </form>
    </div>
  )
}
