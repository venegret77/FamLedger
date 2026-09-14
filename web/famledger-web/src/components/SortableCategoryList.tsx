import { useEffect, useMemo, useState } from 'react'
import {
  DndContext,
  closestCenter,
  KeyboardSensor,
  PointerSensor,
  TouchSensor,
  useSensor,
  useSensors,
  type DragEndEvent,
} from '@dnd-kit/core'
import {
  SortableContext,
  sortableKeyboardCoordinates,
  useSortable,
  verticalListSortingStrategy,
} from '@dnd-kit/sortable'
import { CSS } from '@dnd-kit/utilities'
import { Button } from './ui/Button'
import { moveItemInArray } from '../lib/arrayOrder'

export type SortableCategory = {
  id: string
  name: string
}

type SortableCategoryListProps = {
  categories: SortableCategory[]
  selectedIds: Set<string>
  disabled?: boolean
  editingId: string | null
  editingName: string
  onEditingNameChange: (value: string) => void
  onToggleSelect: (id: string) => void
  onStartEdit: (id: string, name: string) => void
  onCancelEdit: () => void
  onSaveEdit: (id: string) => void
  onDelete: (id: string, name: string) => void
  onReorder: (orderedIds: string[]) => void
  savePending?: boolean
  deletePending?: boolean
}

function DragHandle({ listeners, attributes }: { listeners: object; attributes: object }) {
  return (
    <button
      type="button"
      className="flex size-9 shrink-0 cursor-grab items-center justify-center rounded-lg text-slate-400 hover:bg-slate-100 hover:text-slate-600 active:cursor-grabbing touch-none"
      aria-label="Перетащить"
      title="Перетащить"
      {...attributes}
      {...listeners}
    >
      <svg width="16" height="16" viewBox="0 0 16 16" fill="currentColor" aria-hidden>
        <circle cx="5" cy="4" r="1.25" />
        <circle cx="11" cy="4" r="1.25" />
        <circle cx="5" cy="8" r="1.25" />
        <circle cx="11" cy="8" r="1.25" />
        <circle cx="5" cy="12" r="1.25" />
        <circle cx="11" cy="12" r="1.25" />
      </svg>
    </button>
  )
}

function SortableRow({
  cat,
  selected,
  editing,
  editingName,
  disabled,
  savePending,
  deletePending,
  onToggleSelect,
  onEditingNameChange,
  onStartEdit,
  onCancelEdit,
  onSaveEdit,
  onDelete,
}: {
  cat: SortableCategory
  selected: boolean
  editing: boolean
  editingName: string
  disabled?: boolean
  savePending?: boolean
  deletePending?: boolean
  onToggleSelect: (id: string) => void
  onEditingNameChange: (value: string) => void
  onStartEdit: (id: string, name: string) => void
  onCancelEdit: () => void
  onSaveEdit: (id: string) => void
  onDelete: (id: string, name: string) => void
}) {
  const { attributes, listeners, setNodeRef, transform, transition, isDragging } = useSortable({
    id: cat.id,
    disabled: disabled || editing,
  })

  const style = {
    transform: CSS.Transform.toString(transform),
    transition,
  }

  return (
    <li
      ref={setNodeRef}
      style={style}
      className={`flex items-center justify-between gap-2 px-3 py-3 sm:gap-3 sm:px-4 ${
        isDragging ? 'z-10 rounded-xl bg-white shadow-md ring-1 ring-brand-200' : ''
      }`}
    >
      {editing ? (
        <div className="flex flex-1 items-center gap-2">
          <input
            className="flex-1 rounded-lg border border-slate-200 px-3 py-1.5 text-sm"
            value={editingName}
            onChange={(e) => onEditingNameChange(e.target.value)}
          />
          <Button size="sm" loading={savePending} onClick={() => onSaveEdit(cat.id)}>
            Сохранить
          </Button>
          <Button size="sm" variant="ghost" onClick={onCancelEdit}>
            Отмена
          </Button>
        </div>
      ) : (
        <>
          <div className="flex min-w-0 flex-1 items-center gap-1 sm:gap-2">
            <DragHandle listeners={listeners ?? {}} attributes={attributes} />
            <label className="flex min-w-0 flex-1 cursor-pointer items-center gap-3">
              <input
                type="checkbox"
                className="size-4 shrink-0 rounded border-slate-300"
                checked={selected}
                onChange={() => onToggleSelect(cat.id)}
              />
              <span className="truncate font-medium text-slate-900">{cat.name}</span>
            </label>
          </div>
          <div className="flex shrink-0 items-center gap-1">
            <Button size="sm" variant="ghost" onClick={() => onStartEdit(cat.id, cat.name)}>
              Изменить
            </Button>
            <Button
              size="sm"
              variant="ghost"
              className="text-red-600 hover:bg-red-50"
              loading={deletePending}
              onClick={() => onDelete(cat.id, cat.name)}
            >
              Удалить
            </Button>
          </div>
        </>
      )}
    </li>
  )
}

export function SortableCategoryList(props: SortableCategoryListProps) {
  const {
    categories,
    selectedIds,
    disabled,
    editingId,
    editingName,
    onEditingNameChange,
    onToggleSelect,
    onStartEdit,
    onCancelEdit,
    onSaveEdit,
    onDelete,
    onReorder,
    savePending,
    deletePending,
  } = props

  const [items, setItems] = useState(categories)
  useEffect(() => {
    setItems(categories)
  }, [categories])

  const ids = useMemo(() => items.map((c) => c.id), [items])

  const sensors = useSensors(
    useSensor(PointerSensor, { activationConstraint: { distance: 6 } }),
    useSensor(TouchSensor, { activationConstraint: { delay: 180, tolerance: 6 } }),
    useSensor(KeyboardSensor, { coordinateGetter: sortableKeyboardCoordinates }),
  )

  function handleDragEnd(event: DragEndEvent) {
    const { active, over } = event
    if (!over || active.id === over.id) return

    const fromIndex = items.findIndex((c) => c.id === active.id)
    const toIndex = items.findIndex((c) => c.id === over.id)
    if (fromIndex < 0 || toIndex < 0) return

    const next = moveItemInArray(items, fromIndex, toIndex)
    setItems(next)
    onReorder(next.map((c) => c.id))
  }

  return (
    <DndContext sensors={sensors} collisionDetection={closestCenter} onDragEnd={handleDragEnd}>
      <SortableContext items={ids} strategy={verticalListSortingStrategy}>
        <ul className="mt-3 divide-y divide-slate-100 rounded-xl border border-slate-200 bg-white">
          {items.map((cat) => (
            <SortableRow
              key={cat.id}
              cat={cat}
              selected={selectedIds.has(cat.id)}
              editing={editingId === cat.id}
              editingName={editingName}
              disabled={disabled}
              savePending={savePending}
              deletePending={deletePending}
              onToggleSelect={onToggleSelect}
              onEditingNameChange={onEditingNameChange}
              onStartEdit={onStartEdit}
              onCancelEdit={onCancelEdit}
              onSaveEdit={onSaveEdit}
              onDelete={onDelete}
            />
          ))}
        </ul>
      </SortableContext>
    </DndContext>
  )
}
