import { formatMoney } from '../lib/format'
import type { CategorySpendingLimit } from '../api/types'

export type CategorySpendRow = {
  key: string
  categoryId?: string | null
  name: string
  spent: number
  limitAmount?: number
  percentUsed?: number
  overLimit?: boolean
  nearLimit?: boolean
}

export function buildCategorySpendRows(
  spentByCategoryId: Map<string, { name: string; spent: number }>,
  uncategorizedSpent: number,
  limits: CategorySpendingLimit[] | undefined,
): CategorySpendRow[] {
  const enabledLimits = (limits ?? []).filter((l) => l.isEnabled)
  const limitByCategory = new Map(enabledLimits.map((l) => [l.categoryId, l]))

  const rows: CategorySpendRow[] = []
  const seen = new Set<string>()

  for (const [categoryId, { name, spent }] of spentByCategoryId) {
    seen.add(categoryId)
    const limit = limitByCategory.get(categoryId)
    rows.push(toRow(categoryId, name, spent, limit))
  }

  if (uncategorizedSpent > 0) {
    rows.push({
      key: '__none__',
      categoryId: null,
      name: 'Без категории',
      spent: uncategorizedSpent,
    })
  }

  for (const limit of enabledLimits) {
    if (seen.has(limit.categoryId)) continue
    rows.push(toRow(limit.categoryId, limit.categoryName, 0, limit))
  }

  return rows.sort((a, b) => {
    const aHas = a.limitAmount != null ? 0 : 1
    const bHas = b.limitAmount != null ? 0 : 1
    if (aHas !== bHas) return aHas - bHas
    return b.spent - a.spent || a.name.localeCompare(b.name)
  })
}

function toRow(
  categoryId: string,
  name: string,
  spent: number,
  limit?: CategorySpendingLimit,
): CategorySpendRow {
  if (!limit || limit.limitAmount <= 0) {
    return { key: categoryId, categoryId, name, spent }
  }

  const percentUsed = Math.round((spent / limit.limitAmount) * 100)
  const nearThreshold = Math.min(...limit.thresholdPercents.filter((t) => t < 100), 80)
  return {
    key: categoryId,
    categoryId,
    name,
    spent,
    limitAmount: limit.limitAmount,
    percentUsed,
    overLimit: percentUsed >= 100,
    nearLimit: percentUsed >= nearThreshold,
  }
}

export function CategorySpendProgressList({
  rows,
  currency,
}: {
  rows: CategorySpendRow[]
  currency: string
}) {
  if (rows.length === 0) return null

  return (
    <ul className="mt-4 space-y-4">
      {rows.map((row) => {
        const hasLimit = row.limitAmount != null && row.limitAmount > 0
        const pct = hasLimit
          ? Math.min(100, Math.max(0, row.percentUsed ?? 0))
          : 0
        const barClass = row.overLimit
          ? 'bg-red-500'
          : row.nearLimit
            ? 'bg-amber-500'
            : 'bg-brand-500'

        return (
          <li key={row.key} className="space-y-1.5">
            <div className="flex items-baseline justify-between gap-3 text-sm">
              <span className="min-w-0 truncate font-medium text-slate-900">{row.name}</span>
              <span className="shrink-0 text-slate-900">
                {hasLimit ? (
                  <>
                    {formatMoney(row.spent, currency)}
                    <span className="text-slate-400">
                      {' '}
                      / {formatMoney(row.limitAmount!, currency)}
                    </span>
                  </>
                ) : (
                  formatMoney(row.spent, currency)
                )}
              </span>
            </div>
            {hasLimit && (
              <>
                <div className="h-2 overflow-hidden rounded-full bg-slate-100">
                  <div
                    className={`h-full rounded-full transition-all ${barClass}`}
                    style={{ width: `${pct}%` }}
                  />
                </div>
                <p
                  className={`text-xs ${
                    row.overLimit
                      ? 'text-red-600'
                      : row.nearLimit
                        ? 'text-amber-700'
                        : 'text-slate-500'
                  }`}
                >
                  {row.percentUsed ?? 0}% лимита
                  {row.overLimit ? ' · превышен' : ''}
                </p>
              </>
            )}
          </li>
        )
      })}
    </ul>
  )
}
