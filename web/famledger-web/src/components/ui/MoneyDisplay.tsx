import { formatMoney } from '../../lib/format'

interface MoneyDisplayProps {
  amount: number
  currency?: string
  size?: 'sm' | 'md' | 'lg' | 'xl'
  className?: string
  signed?: boolean
}

const sizeClasses = {
  sm: 'text-sm',
  md: 'text-lg',
  lg: 'text-2xl',
  xl: 'text-3xl sm:text-4xl',
}

export function MoneyDisplay({
  amount,
  currency = 'RSD',
  size = 'md',
  className = '',
  signed = false,
}: MoneyDisplayProps) {
  const formatted = formatMoney(Math.abs(amount), currency)
  const prefix = signed && amount > 0 ? '+' : signed && amount < 0 ? '−' : ''
  const color =
    signed && amount < 0
      ? 'text-red-600'
      : signed && amount > 0
        ? 'text-emerald-600'
        : 'text-slate-900'

  return (
    <span className={`font-semibold tabular-nums tracking-tight ${sizeClasses[size]} ${color} ${className}`}>
      {prefix}
      {formatted}
    </span>
  )
}

interface StatCardProps {
  label: string
  amount: number
  currency?: string
  hint?: string
  breakdown?: { label: string; amount: number }[]
  accent?: boolean
}

export function StatCard({
  label,
  amount,
  currency = 'RSD',
  hint,
  breakdown,
  accent = false,
}: StatCardProps) {
  return (
    <div
      className={`rounded-2xl p-4 sm:p-5 ${
        accent
          ? 'bg-gradient-to-br from-brand-600 to-brand-700 text-white shadow-md shadow-brand-600/20'
          : 'border border-slate-200/80 bg-white shadow-sm'
      }`}
    >
      <p
        className={`text-xs font-medium uppercase tracking-wide sm:text-sm ${
          accent ? 'text-brand-100' : 'text-slate-500'
        }`}
      >
        {label}
      </p>
      <p
        className={`mt-1 text-xl font-bold tabular-nums sm:text-2xl ${
          accent ? 'text-white' : 'text-slate-900'
        }`}
      >
        {formatMoney(amount, currency)}
      </p>
      {breakdown && breakdown.length > 0 && (
        <dl
          className={`mt-3 space-y-1 border-t pt-2 text-xs ${
            accent ? 'border-white/20 text-brand-100' : 'border-slate-100 text-slate-500'
          }`}
        >
          {breakdown.map((row) => (
            <div key={row.label} className="flex items-baseline justify-between gap-3">
              <dt>{row.label}</dt>
              <dd className={`tabular-nums font-medium ${accent ? 'text-white/90' : 'text-slate-700'}`}>
                {formatMoney(row.amount, currency)}
              </dd>
            </div>
          ))}
        </dl>
      )}
      {hint && !breakdown && (
        <p className={`mt-1 text-xs ${accent ? 'text-brand-100' : 'text-slate-500'}`}>
          {hint}
        </p>
      )}
    </div>
  )
}
