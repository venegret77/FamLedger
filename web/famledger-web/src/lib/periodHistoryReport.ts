import type { PeriodHistoryDetail, Transaction } from '../api/types'
import { formatDate, formatMoney } from './format'

function escapeCell(value: string): string {
  return value.replace(/\|/g, '\\|').replace(/\r?\n/g, ' ')
}

function titleFor(tx: Transaction): string {
  if (tx.note) return tx.note
  if (tx.categoryName) return tx.categoryName
  return (tx.kind ?? 'Expense') === 'Income' ? 'Пополнение' : 'Расход'
}

function kindLabel(tx: Transaction): string {
  return (tx.kind ?? 'Expense') === 'Income' ? 'Пополнение' : 'Расход'
}

function signedAmount(tx: Transaction): number {
  return (tx.kind ?? 'Expense') === 'Income' ? tx.amount : -tx.amount
}

/** Markdown-отчёт по периоду: сводка, категории, дни, операции — для просмотра и AI-анализа. */
export function buildPeriodHistoryMarkdown(
  detail: PeriodHistoryDetail,
  transactions: Transaction[] | undefined,
): string {
  const currency = detail.currency
  const status = detail.isActive ? 'Текущий' : 'Закрыт'
  const lines: string[] = [
    `# FamLedger — ${detail.label}`,
    '',
    `- **Период:** ${formatDate(detail.startDate)} — ${formatDate(detail.endDate)}`,
    `- **Статус:** ${status}`,
    `- **Валюта учёта:** ${currency}`,
    `- **Дней в периоде:** ${detail.daysInPeriod}`,
    `- **Операций:** ${detail.transactionCount} (списаний: ${detail.expenseCount}, пополнений: ${detail.incomeCount})`,
  ]

  if (detail.closedAt) {
    lines.push(`- **Закрыт:** ${formatDate(detail.closedAt)}`)
  }

  lines.push(
    '',
    '## Сводка',
    '',
    '| Показатель | Сумма |',
    '|---|---|',
    `| Доходы (план) | ${formatMoney(detail.income, currency)} |`,
    `| Пополнения | ${formatMoney(detail.topUps, currency)} |`,
    `| План расходов | ${formatMoney(detail.plannedExpenses, currency)} |`,
    `| Факт расходов | ${formatMoney(detail.spent, currency)} |`,
    `| Остаток | ${formatMoney(detail.remaining, currency)} |`,
    `| Дневной бюджет | ${formatMoney(detail.dailyBudget, currency)} |`,
    '',
    '## Расходы по категориям',
    '',
  )

  if (!detail.byCategory.length) {
    lines.push('_Нет расходов по категориям._', '')
  } else {
    lines.push('| Категория | Операций | Сумма |', '|---|---:|---:|')
    for (const item of detail.byCategory) {
      lines.push(
        `| ${escapeCell(item.name)} | ${item.count} | ${formatMoney(item.amount, currency)} |`,
      )
    }
    lines.push('')
  }

  lines.push('## По дням', '')

  if (!detail.byDay.length) {
    lines.push('_Нет данных по дням._', '')
  } else {
    lines.push('| Дата | Расход | Пополнения |', '|---|---:|---:|')
    for (const day of detail.byDay) {
      lines.push(
        `| ${formatDate(day.date)} | ${formatMoney(day.spent, currency)} | ${formatMoney(day.topUps, currency)} |`,
      )
    }
    lines.push('')
  }

  lines.push('## Операции', '')

  const list = transactions ?? []
  if (!list.length) {
    lines.push('_Операций в этом периоде нет._', '')
  } else {
    lines.push(
      '| Дата | Тип | Описание | Категория | Сумма | Валюта | Кто |',
      '|---|---|---|---|---:|---|---|',
    )
    for (const tx of list) {
      lines.push(
        `| ${formatDate(tx.date)} | ${kindLabel(tx)} | ${escapeCell(titleFor(tx))} | ${escapeCell(tx.categoryName ?? '—')} | ${formatMoney(signedAmount(tx), tx.currency)} | ${tx.currency} | ${escapeCell(tx.createdByName ?? '—')} |`,
      )
    }
    lines.push('')
  }

  lines.push(
    '---',
    '',
    '_Отчёт сформирован в FamLedger для просмотра и анализа._',
    '',
  )

  return lines.join('\n')
}

export function periodReportFileName(detail: PeriodHistoryDetail): string {
  const safe = detail.label
    .replace(/[^\p{L}\p{N}]+/gu, '-')
    .replace(/^-|-$/g, '')
    .toLowerCase()
  return `famledger-${safe || detail.id}.md`
}

export function downloadTextFile(filename: string, content: string, mime = 'text/markdown;charset=utf-8') {
  const blob = new Blob([content], { type: mime })
  const url = URL.createObjectURL(blob)
  const anchor = document.createElement('a')
  anchor.href = url
  anchor.download = filename
  anchor.rel = 'noopener'
  document.body.appendChild(anchor)
  anchor.click()
  document.body.removeChild(anchor)
  URL.revokeObjectURL(url)
}
