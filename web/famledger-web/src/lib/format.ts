const currencyFormatterCache = new Map<string, Intl.NumberFormat>()

function fractionDigitsFor(currency: string): number {
  return currency.toUpperCase() === 'RSD' ? 0 : 2
}

export function formatMoney(
  amount: number,
  currency = 'RSD',
  locale = 'ru-RU',
): string {
  const code = currency.toUpperCase()
  const key = `${locale}:${code}`
  let formatter = currencyFormatterCache.get(key)

  if (!formatter) {
    const digits = fractionDigitsFor(code)
    formatter = new Intl.NumberFormat(locale, {
      style: 'currency',
      currency: code,
      minimumFractionDigits: digits,
      maximumFractionDigits: digits,
    })
    currencyFormatterCache.set(key, formatter)
  }

  return formatter.format(amount)
}

export function formatDate(date: string, locale = 'ru-RU'): string {
  return new Intl.DateTimeFormat(locale, {
    day: 'numeric',
    month: 'short',
  }).format(new Date(date))
}

export function formatDateTime(date: string, locale = 'ru-RU'): string {
  return new Intl.DateTimeFormat(locale, {
    day: 'numeric',
    month: 'short',
    hour: '2-digit',
    minute: '2-digit',
  }).format(new Date(date))
}

export function roleLabel(role: string): string {
  switch (role) {
    case 'Head':
      return 'Глава'
    case 'Assistant':
      return 'Помощник'
    case 'Member':
      return 'Участник'
    default:
      return role
  }
}
