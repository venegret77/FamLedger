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
