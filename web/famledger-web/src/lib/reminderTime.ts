import type { ReminderSchedule } from '../api/types'

/** "HH:mm" helpers: UI shows local time, API stores UTC. */

function pad(n: number): string {
  return String(n).padStart(2, '0')
}

export function formatTimeHm(hours: number, minutes: number): string {
  return `${pad(hours)}:${pad(minutes)}`
}

/** Local wall-clock HH:mm → UTC HH:mm (using today's date for offset). */
export function localTimeToUtc(localHm: string): string {
  const [h, m] = localHm.split(':').map(Number)
  const d = new Date()
  d.setHours(h, m, 0, 0)
  return formatTimeHm(d.getUTCHours(), d.getUTCMinutes())
}

/** UTC HH:mm → local wall-clock HH:mm. */
export function utcTimeToLocal(utcHm: string): string {
  const [h, m] = utcHm.split(':').map(Number)
  const d = new Date()
  d.setUTCHours(h, m, 0, 0)
  return formatTimeHm(d.getHours(), d.getMinutes())
}

export function currentLocalTimeHm(): string {
  const d = new Date()
  return formatTimeHm(d.getHours(), d.getMinutes())
}

export const WEEKDAYS = [
  { day: 1, short: 'Пн', label: 'Понедельник' },
  { day: 2, short: 'Вт', label: 'Вторник' },
  { day: 3, short: 'Ср', label: 'Среда' },
  { day: 4, short: 'Чт', label: 'Четверг' },
  { day: 5, short: 'Пт', label: 'Пятница' },
  { day: 6, short: 'Сб', label: 'Суббота' },
  { day: 7, short: 'Вс', label: 'Воскресенье' },
] as const

export interface LocalDayDraft {
  day: number
  enabled: boolean
  timeLocal: string
}

export interface ScheduleDraft {
  sameTimeEveryDay: boolean
  sharedTimeLocal: string
  days: LocalDayDraft[]
}

function isoWeekday(date: Date, utc: boolean): number {
  const day = utc ? date.getUTCDay() : date.getDay()
  return day === 0 ? 7 : day
}

/** Local weekday + wall time → UTC weekday + wall time. */
export function localSlotToUtc(isoDay: number, localHm: string): { day: number; timeUtc: string } {
  const [hours, minutes] = localHm.split(':').map(Number)
  const local = new Date()
  local.setDate(local.getDate() + (isoDay - isoWeekday(local, false)))
  local.setHours(hours, minutes, 0, 0)
  return {
    day: isoWeekday(local, true),
    timeUtc: formatTimeHm(local.getUTCHours(), local.getUTCMinutes()),
  }
}

/** UTC weekday + wall time → local weekday + wall time. */
export function utcSlotToLocal(isoDay: number, utcHm: string): { day: number; timeLocal: string } {
  const [hours, minutes] = utcHm.split(':').map(Number)
  const utc = new Date()
  utc.setUTCDate(utc.getUTCDate() + (isoDay - isoWeekday(utc, true)))
  utc.setUTCHours(hours, minutes, 0, 0)
  return {
    day: isoWeekday(utc, false),
    timeLocal: formatTimeHm(utc.getHours(), utc.getMinutes()),
  }
}

export function emptyScheduleDraft(timeLocal = currentLocalTimeHm()): ScheduleDraft {
  return {
    sameTimeEveryDay: true,
    sharedTimeLocal: timeLocal,
    days: WEEKDAYS.map((day) => ({
      day: day.day,
      enabled: true,
      timeLocal,
    })),
  }
}

export function scheduleFromApi(
  schedule: ReminderSchedule | null | undefined,
  fallbackTimeUtc?: string | null,
): ScheduleDraft {
  const fallbackLocal = fallbackTimeUtc ? utcTimeToLocal(fallbackTimeUtc) : currentLocalTimeHm()
  if (!schedule?.days?.length) {
    return emptyScheduleDraft(fallbackLocal)
  }

  const byLocalDay = new Map<number, string>()
  for (const slot of schedule.days) {
    if (!slot.enabled) continue
    const local = utcSlotToLocal(slot.day, slot.timeUtc)
    byLocalDay.set(local.day, local.timeLocal)
  }

  const firstTime = byLocalDay.values().next().value ?? fallbackLocal
  return {
    sameTimeEveryDay: schedule.sameTimeEveryDay,
    sharedTimeLocal: firstTime,
    days: WEEKDAYS.map((day) => ({
      day: day.day,
      enabled: byLocalDay.has(day.day),
      timeLocal: byLocalDay.get(day.day) ?? firstTime,
    })),
  }
}

export function draftToApi(draft: ScheduleDraft): ReminderSchedule {
  return {
    sameTimeEveryDay: draft.sameTimeEveryDay,
    days: draft.days
      .filter((day) => day.enabled)
      .map((day) => {
        const localTime = draft.sameTimeEveryDay ? draft.sharedTimeLocal : day.timeLocal
        const utc = localSlotToUtc(day.day, localTime)
        return { day: utc.day, enabled: true, timeUtc: utc.timeUtc }
      }),
  }
}

export function formatScheduleSummary(
  schedule: ReminderSchedule | null | undefined,
  fallbackTimeUtc?: string | null,
): string {
  const draft = scheduleFromApi(schedule, fallbackTimeUtc)
  const enabled = draft.days.filter((day) => day.enabled)
  if (enabled.length === 0) return 'Дни не выбраны'

  const labels = Object.fromEntries(WEEKDAYS.map((day) => [day.day, day.short]))
  if (draft.sameTimeEveryDay) {
    if (enabled.length === 7) return `Каждый день в ${draft.sharedTimeLocal}`
    return `${enabled.map((day) => labels[day.day]).join(', ')} в ${draft.sharedTimeLocal}`
  }

  return enabled.map((day) => `${labels[day.day]} ${day.timeLocal}`).join(', ')
}
