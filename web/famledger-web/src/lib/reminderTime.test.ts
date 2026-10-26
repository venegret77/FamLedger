import { describe, expect, it } from 'vitest'
import {
  draftToApi,
  localSlotToUtc,
  scheduleFromApi,
  utcSlotToLocal,
} from './reminderTime'

describe('reminder weekday conversion', () => {
  it('roundtrips every local weekday and time', () => {
    for (let day = 1; day <= 7; day += 1) {
      const utc = localSlotToUtc(day, '09:30')
      expect(utcSlotToLocal(utc.day, utc.timeUtc)).toEqual({
        day,
        timeLocal: '09:30',
      })
    }
  })

  it('keeps the same UTC clock time for one local time', () => {
    const monday = localSlotToUtc(1, '21:15')
    const friday = localSlotToUtc(5, '21:15')
    expect(monday.timeUtc).toBe(friday.timeUtc)
  })

  it('restores the same-time draft after an API roundtrip', () => {
    const api = draftToApi({
      sameTimeEveryDay: true,
      sharedTimeLocal: '08:00',
      days: [
        { day: 1, enabled: true, timeLocal: '08:00' },
        { day: 2, enabled: false, timeLocal: '08:00' },
        { day: 3, enabled: true, timeLocal: '11:00' },
        { day: 4, enabled: false, timeLocal: '08:00' },
        { day: 5, enabled: false, timeLocal: '08:00' },
        { day: 6, enabled: false, timeLocal: '08:00' },
        { day: 7, enabled: false, timeLocal: '08:00' },
      ],
    })

    const restored = scheduleFromApi(api)
    expect(restored.sameTimeEveryDay).toBe(true)
    expect(restored.sharedTimeLocal).toBe('08:00')
    expect(restored.days.filter((day) => day.enabled).map((day) => day.day)).toEqual([1, 3])
  })
})
