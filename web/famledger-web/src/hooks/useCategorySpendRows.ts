import { useMemo } from 'react'
import type { CategorySpendingLimit, TransactionTotals } from '../api/types'
import { buildCategorySpendRows, type CategorySpendRow } from '../components/CategorySpendProgress'

export function useCategorySpendRows(
  categories: TransactionTotals['categories'] | undefined,
  limits: CategorySpendingLimit[] | undefined,
): CategorySpendRow[] {
  return useMemo(() => {
    const spentByCategoryId = new Map<string, { name: string; spent: number }>()
    let uncategorizedSpent = 0

    for (const category of categories ?? []) {
      if (!category.categoryId) {
        uncategorizedSpent += category.spent
        continue
      }
      spentByCategoryId.set(category.categoryId, {
        name: category.name ?? 'Категория',
        spent: category.spent,
      })
    }

    return buildCategorySpendRows(spentByCategoryId, uncategorizedSpent, limits)
  }, [categories, limits])
}
