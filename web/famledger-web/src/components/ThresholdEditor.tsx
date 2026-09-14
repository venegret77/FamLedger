import { useState } from 'react'
import { Button } from './ui/Button'
import { Input } from './ui/Input'

export function normalizeThresholds(values: number[]): number[] {
  return [...new Set(values.filter((n) => n >= 1 && n <= 100))]
    .sort((a, b) => a - b)
    .slice(0, 10)
}

export function ThresholdEditor({
  values,
  onChange,
  disabled,
}: {
  values: number[]
  onChange: (next: number[]) => void
  disabled?: boolean
}) {
  const [draft, setDraft] = useState('')

  function addThreshold() {
    const n = Number.parseInt(draft, 10)
    if (Number.isNaN(n) || n < 1 || n > 100) return
    onChange(normalizeThresholds([...values, n]))
    setDraft('')
  }

  return (
    <div className="space-y-2">
      <p className="text-sm font-medium text-slate-700">Пороги %</p>
      <div className="flex flex-wrap gap-2">
        {values.map((t) => (
          <button
            key={t}
            type="button"
            disabled={disabled}
            onClick={() => onChange(values.filter((x) => x !== t))}
            className="inline-flex items-center gap-1 rounded-md border border-slate-200 bg-slate-50 px-2 py-1 text-sm text-slate-700 hover:bg-slate-100 disabled:opacity-50"
            title="Убрать порог"
          >
            {t}%
            <span aria-hidden className="text-slate-400">
              ×
            </span>
          </button>
        ))}
        {values.length === 0 && (
          <span className="text-sm text-slate-400">Нет порогов</span>
        )}
      </div>
      <div className="flex max-w-xs gap-2">
        <Input
          type="number"
          min={1}
          max={100}
          value={draft}
          placeholder="например 50"
          disabled={disabled || values.length >= 10}
          onChange={(e) => setDraft(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === 'Enter') {
              e.preventDefault()
              addThreshold()
            }
          }}
        />
        <Button
          type="button"
          variant="secondary"
          disabled={disabled || values.length >= 10}
          onClick={addThreshold}
        >
          Добавить
        </Button>
      </div>
    </div>
  )
}
