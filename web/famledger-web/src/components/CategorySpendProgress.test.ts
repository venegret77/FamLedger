import { describe, expect, it } from 'vitest'
import { buildCategorySpendRows } from '../components/CategorySpendProgress'
import type { CategorySpendingLimit } from '../api/types'

function limit(
  partial: Partial<CategorySpendingLimit> & Pick<CategorySpendingLimit, 'categoryId' | 'categoryName' | 'limitAmount'>,
): CategorySpendingLimit {
  return {
    id: partial.id ?? partial.categoryId,
    categoryId: partial.categoryId,
    categoryName: partial.categoryName,
    limitAmount: partial.limitAmount,
    thresholdPercents: partial.thresholdPercents ?? [25, 50, 75, 95],
    audience: partial.audience ?? 'Self',
    isEnabled: partial.isEnabled ?? true,
    createdByUserId: 'u1',
    canEdit: true,
    createdAtUtc: '',
    updatedAtUtc: '',
  }
}

describe('buildCategorySpendRows', () => {
  it('joins spend with limits and sorts limited first', () => {
    const spent = new Map([
      ['food', { name: 'Продукты', spent: 800 }],
      ['taxi', { name: 'Такси', spent: 100 }],
    ])
    const rows = buildCategorySpendRows(spent, 50, [
      limit({ categoryId: 'food', categoryName: 'Продукты', limitAmount: 1000 }),
    ])

    expect(rows[0]?.name).toBe('Продукты')
    expect(rows[0]?.percentUsed).toBe(80)
    expect(rows[0]?.nearLimit).toBe(true)
    expect(rows.some((r) => r.name === 'Без категории' && r.spent === 50)).toBe(true)
    expect(rows.some((r) => r.name === 'Такси' && r.limitAmount == null)).toBe(true)
  })

  it('includes enabled limits with zero spend', () => {
    const rows = buildCategorySpendRows(new Map(), 0, [
      limit({ categoryId: 'fun', categoryName: 'Развлечения', limitAmount: 500 }),
    ])
    expect(rows).toHaveLength(1)
    expect(rows[0]?.spent).toBe(0)
    expect(rows[0]?.percentUsed).toBe(0)
  })

  it('marks over-limit when spent exceeds cap', () => {
    const spent = new Map([['food', { name: 'Продукты', spent: 1200 }]])
    const rows = buildCategorySpendRows(spent, 0, [
      limit({ categoryId: 'food', categoryName: 'Продукты', limitAmount: 1000 }),
    ])
    expect(rows[0]?.overLimit).toBe(true)
    expect(rows[0]?.percentUsed).toBe(120)
  })
})
