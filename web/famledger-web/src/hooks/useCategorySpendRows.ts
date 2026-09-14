import { useMemo } from 'react'
import type { CategorySpendingLimit, Transaction } from '../api/types'
import { buildCategorySpendRows, type CategorySpendRow } from '../components/CategorySpendProgress'

export function useCategorySpendRows(
  transactions: Transaction[] | undefined,
  limits: CategorySpendingLimit[] | undefined,
): CategorySpendRow[] {
  return useMemo(() => {
    const spentByCategoryId = new Map<string, { name: string; spent: number }>()
    let uncategorizedSpent = 0

    for (const tx of transactions ?? []) {
      if ((tx.kind ?? 'Expense') !== 'Expense') continue
      if (!tx.categoryId) {
        uncategorizedSpent += tx.baseAmount
        continue
      }
      const prev = spentByCategoryId.get(tx.categoryId)
      if (prev) {
        prev.spent += tx.baseAmount
      } else {
        spentByCategoryId.set(tx.categoryId, {
          name: tx.categoryName ?? 'Категория',
          spent: tx.baseAmount,
        })
      }
    }

    return buildCategorySpendRows(spentByCategoryId, uncategorizedSpent, limits)
  }, [transactions, limits])
}
