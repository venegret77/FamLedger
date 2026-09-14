import { Link } from 'react-router-dom'
import { formatMoney } from '../lib/format'
import type { CategorySpendRow } from './CategorySpendProgress'
import { Card } from './ui/Card'

function toneFor(row: CategorySpendRow) {
  if (row.overLimit) {
    return {
      ring: 'stroke-red-500',
      text: 'text-red-600',
      track: 'stroke-red-100',
      chip: 'bg-red-50 text-red-700',
      bar: 'bg-red-500',
    }
  }
  if (row.nearLimit) {
    return {
      ring: 'stroke-amber-500',
      text: 'text-amber-700',
      track: 'stroke-amber-100',
      chip: 'bg-amber-50 text-amber-800',
      bar: 'bg-amber-500',
    }
  }
  return {
    ring: 'stroke-brand-500',
    text: 'text-brand-700',
    track: 'stroke-brand-100',
    chip: 'bg-brand-50 text-brand-800',
    bar: 'bg-brand-500',
  }
}

function LimitRing({ percent, className }: { percent: number; className: string }) {
  const size = 88
  const stroke = 8
  const radius = (size - stroke) / 2
  const circumference = 2 * Math.PI * radius
  const clamped = Math.min(100, Math.max(0, percent))
  const offset = circumference - (clamped / 100) * circumference

  return (
    <svg width={size} height={size} className="-rotate-90" aria-hidden>
      <circle
        cx={size / 2}
        cy={size / 2}
        r={radius}
        fill="none"
        strokeWidth={stroke}
        className="stroke-slate-100"
      />
      <circle
        cx={size / 2}
        cy={size / 2}
        r={radius}
        fill="none"
        strokeWidth={stroke}
        strokeLinecap="round"
        strokeDasharray={circumference}
        strokeDashoffset={offset}
        className={`transition-[stroke-dashoffset] duration-500 ease-out ${className}`}
      />
    </svg>
  )
}

export function DashboardCategorySpend({
  rows,
  currency,
}: {
  rows: CategorySpendRow[]
  currency: string
}) {
  if (rows.length === 0) return null

  const withLimits = rows.filter((r) => r.limitAmount != null && r.limitAmount > 0)
  const withoutLimits = rows.filter((r) => r.limitAmount == null || r.limitAmount <= 0)
  const alertCount = withLimits.filter((r) => r.overLimit || r.nearLimit).length

  return (
    <section className="space-y-3">
      <div className="flex flex-wrap items-end justify-between gap-2">
        <div>
          <h2 className="text-base font-semibold text-slate-900">Категории периода</h2>
          <p className="mt-0.5 text-sm text-slate-500">
            {withLimits.length > 0
              ? alertCount > 0
                ? `${alertCount} ${alertCount === 1 ? 'лимит' : 'лимита'} требуют внимания`
                : 'Прогресс по лимитам и расходам'
              : 'Суммы расходов по категориям'}
          </p>
        </div>
        {withLimits.length > 0 && (
          <Link
            to="/settings?tab=categories"
            className="text-sm font-medium text-brand-700 hover:text-brand-800"
          >
            Настроить лимиты
          </Link>
        )}
      </div>

      {withLimits.length > 0 && (
        <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
          {withLimits.map((row) => {
            const tone = toneFor(row)
            const pct = Math.min(100, Math.max(0, row.percentUsed ?? 0))
            const remaining = Math.max(0, (row.limitAmount ?? 0) - row.spent)

            return (
              <Card
                key={row.key}
                className={`relative overflow-hidden ${
                  row.overLimit
                    ? 'border-red-200 bg-gradient-to-br from-white to-red-50/80'
                    : row.nearLimit
                      ? 'border-amber-200 bg-gradient-to-br from-white to-amber-50/70'
                      : 'bg-gradient-to-br from-white to-brand-50/40'
                }`}
              >
                <div className="flex items-center gap-4">
                  <div className="relative shrink-0">
                    <LimitRing percent={pct} className={tone.ring} />
                    <div className="absolute inset-0 flex flex-col items-center justify-center">
                      <span className={`text-lg font-bold tabular-nums ${tone.text}`}>
                        {row.percentUsed ?? 0}%
                      </span>
                    </div>
                  </div>
                  <div className="min-w-0 flex-1 space-y-1">
                    <div className="flex flex-wrap items-center gap-2">
                      <p className="truncate font-semibold text-slate-900">{row.name}</p>
                      {row.overLimit && (
                        <span className={`rounded-md px-1.5 py-0.5 text-[11px] font-medium ${tone.chip}`}>
                          Превышен
                        </span>
                      )}
                      {!row.overLimit && row.nearLimit && (
                        <span className={`rounded-md px-1.5 py-0.5 text-[11px] font-medium ${tone.chip}`}>
                          Порог
                        </span>
                      )}
                    </div>
                    <p className="text-sm tabular-nums text-slate-700">
                      <span className="font-semibold text-slate-900">
                        {formatMoney(row.spent, currency)}
                      </span>
                      <span className="text-slate-400">
                        {' '}
                        / {formatMoney(row.limitAmount!, currency)}
                      </span>
                    </p>
                    <p className="text-xs text-slate-500">
                      {row.overLimit
                        ? `Сверх лимита ${formatMoney(row.spent - row.limitAmount!, currency)}`
                        : `Осталось ${formatMoney(remaining, currency)}`}
                    </p>
                  </div>
                </div>
                <div className="mt-4 h-1.5 overflow-hidden rounded-full bg-slate-100/90">
                  <div
                    className={`h-full rounded-full transition-all duration-500 ${tone.bar}`}
                    style={{ width: `${pct}%` }}
                  />
                </div>
              </Card>
            )
          })}
        </div>
      )}

      {withoutLimits.length > 0 && (
        <Card padding="none">
          {withLimits.length > 0 && (
            <div className="border-b border-slate-100 px-5 py-3">
              <p className="text-sm font-medium text-slate-700">Остальные расходы</p>
            </div>
          )}
          <ul className="divide-y divide-slate-100">
            {withoutLimits.map((row) => (
              <li
                key={row.key}
                className="flex items-center justify-between gap-3 px-5 py-3 text-sm"
              >
                <span className="truncate text-slate-700">{row.name}</span>
                <span className="shrink-0 font-semibold tabular-nums text-slate-900">
                  {formatMoney(row.spent, currency)}
                </span>
              </li>
            ))}
          </ul>
        </Card>
      )}
    </section>
  )
}
