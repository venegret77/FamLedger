const currencyFormatterCache = new Map<string, Intl.NumberFormat>()

export function formatMoney(
  amount: number,
  currency = 'RSD',
  locale = 'ru-RU',
): string {
  const key = `${locale}:${currency}`
  let formatter = currencyFormatterCache.get(key)

  if (!formatter) {
    formatter = new Intl.NumberFormat(locale, {
      style: 'currency',
      currency,
      minimumFractionDigits: 0,
      maximumFractionDigits: 0,
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
