import { useEffect, useMemo, useState, type FormEvent } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import {
  useCategoryLimits,
  useContexts,
  useCreateCategory,
  useCreateCategoryLimit,
  useDeleteCategories,
  useDeleteCategory,
  useDeleteCategoryLimit,
  useLogout,
  useMe,
  useReorderCategories,
  useSettings,
  useSwitchContext,
  useTransactions,
  useUpdateBudgetSettings,
  useUpdateCategory,
  useUpdateCategoryLimit,
  useUpdateProfile,
  useUploadAvatar,
} from '../api/hooks'
import { currencyOptions } from '../api/types'
import type { CategorySpendingLimit, ReminderAudience } from '../api/types'
import { Card, CardDescription, CardTitle } from '../components/ui/Card'
import { Button } from '../components/ui/Button'
import { useConfirmDialog } from '../components/ui/ConfirmDialog'
import { Input, Select } from '../components/ui/Input'
import { EmptyState, PageHeader, Spinner, Tabs, Badge } from '../components/ui/Tabs'
import { MobileMoreMenu } from '../components/layout/Navigation'
import {
  ThresholdEditor,
  normalizeThresholds,
} from '../components/ThresholdEditor'
import { CategorySpendProgressList } from '../components/CategorySpendProgress'
import { SortableCategoryList } from '../components/SortableCategoryList'
import { useCategorySpendRows } from '../hooks/useCategorySpendRows'
import { useToast } from '../components/ui/Toast'
import { ApiError } from '../api/client'

const DEFAULT_CATEGORY_THRESHOLDS = [25, 50, 75, 95]

type SettingsTab = 'general' | 'categories'

export function SettingsPage() {
  const navigate = useNavigate()
  const [searchParams, setSearchParams] = useSearchParams()
  const tabParam = searchParams.get('tab')
  const activeTab: SettingsTab =
    tabParam === 'categories' ? 'categories' : 'general'

  function setTab(id: string) {
    const next = id === 'categories' ? 'categories' : 'general'
    setSearchParams(next === 'general' ? {} : { tab: next }, { replace: true })
  }

  const { confirm } = useConfirmDialog()
  const { showToast } = useToast()
  const { data: user } = useMe()
  const { data: settings, isLoading, isError, refetch } = useSettings()
  const { data: contexts } = useContexts()
  const logout = useLogout()
  const createCategory = useCreateCategory()
  const updateCategory = useUpdateCategory()
  const deleteCategory = useDeleteCategory()
  const deleteCategories = useDeleteCategories()
  const reorderCategories = useReorderCategories()
  const updateProfile = useUpdateProfile()
  const uploadAvatar = useUploadAvatar()
  const updateBudget = useUpdateBudgetSettings()
  const switchContext = useSwitchContext()

  const {
    data: categoryLimits,
    isLoading: limitsLoading,
  } = useCategoryLimits()
  const { data: transactions } = useTransactions()
  const createLimit = useCreateCategoryLimit()
  const updateLimit = useUpdateCategoryLimit()
  const deleteLimit = useDeleteCategoryLimit()
  const limitProgressRows = useCategorySpendRows(transactions, categoryLimits).filter(
    (r) => r.limitAmount != null,
  )

  const [displayName, setDisplayName] = useState('')
  const [periodStartDay, setPeriodStartDay] = useState('15')
  const [baseCurrency, setBaseCurrency] = useState('RSD')
  const [newCategoryName, setNewCategoryName] = useState('')
  const [editingId, setEditingId] = useState<string | null>(null)
  const [editingName, setEditingName] = useState('')
  const [selectedCategoryIds, setSelectedCategoryIds] = useState<Set<string>>(new Set())

  const [showLimitForm, setShowLimitForm] = useState(false)
  const [editingLimit, setEditingLimit] = useState<CategorySpendingLimit | null>(null)
  const [limitCategoryId, setLimitCategoryId] = useState('')
  const [limitAmount, setLimitAmount] = useState('')
  const [limitThresholds, setLimitThresholds] = useState<number[]>(DEFAULT_CATEGORY_THRESHOLDS)
  const [limitAudience, setLimitAudience] = useState<ReminderAudience>('Self')
  const [limitEnabled, setLimitEnabled] = useState(true)

  useEffect(() => {
    if (user?.displayName) setDisplayName(user.displayName)
  }, [user?.displayName])

  useEffect(() => {
    if (settings) {
      setPeriodStartDay(String(settings.periodStartDay))
      setBaseCurrency(settings.baseCurrency)
    }
  }, [settings])

  const isPersonal = settings?.isPersonal ?? true
  const audienceOptions = [
    { value: 'Self', label: 'Только я' },
    ...(!isPersonal ? [{ value: 'Family', label: 'Вся семья' }] : []),
  ]

  const categories = useMemo(() => {
    const list = settings?.categories ?? []
    return [...list].sort(
      (a, b) => (a.sortOrder ?? 0) - (b.sortOrder ?? 0) || a.name.localeCompare(b.name),
    )
  }, [settings?.categories])

  const expenseCategories = useMemo(
    () => categories.filter((c) => !c.kind || c.kind === 'Expense'),
    [categories],
  )

  async function handleLogout() {
    try {
      await logout.mutateAsync()
    } finally {
      navigate('/login', { replace: true })
    }
  }

  async function handleAddCategory(event: FormEvent) {
    event.preventDefault()
    const name = newCategoryName.trim()
    if (!name) return
    await createCategory.mutateAsync(name)
    setNewCategoryName('')
  }

  async function handleSaveCategory(id: string) {
    const name = editingName.trim()
    if (!name) return
    await updateCategory.mutateAsync({ id, name })
    setEditingId(null)
    setEditingName('')
  }

  function toggleCategory(id: string) {
    setSelectedCategoryIds((prev) => {
      const next = new Set(prev)
      if (next.has(id)) next.delete(id)
      else next.add(id)
      return next
    })
  }

  function toggleAllCategories(categoryIds: string[]) {
    setSelectedCategoryIds((prev) => {
      if (categoryIds.length > 0 && categoryIds.every((id) => prev.has(id))) {
        return new Set()
      }
      return new Set(categoryIds)
    })
  }

  async function handleBulkDeleteCategories() {
    const ids = [...selectedCategoryIds]
    if (ids.length === 0) return
    const accepted = await confirm({
      title: `Удалить выбранные категории (${ids.length})?`,
      message: 'Категории будут удалены сразу. У связанных операций категория очистится.',
    })
    if (!accepted) return
    await deleteCategories.mutateAsync(ids)
    setSelectedCategoryIds(new Set())
  }

  async function handleReorderCategories(orderedIds: string[]) {
    await reorderCategories.mutateAsync(orderedIds)
  }

  function openCreateLimit() {
    const used = new Set((categoryLimits ?? []).map((l) => l.categoryId))
    const available = expenseCategories.filter((c) => !used.has(c.id))
    if (available.length === 0) {
      showToast({
        title: 'Нет свободных категорий',
        message: 'Лимит уже задан для всех категорий расходов.',
        tone: 'warning',
      })
      return
    }

    setEditingLimit(null)
    setLimitCategoryId(available[0]!.id)
    setLimitAmount('')
    setLimitThresholds([...DEFAULT_CATEGORY_THRESHOLDS])
    setLimitAudience('Self')
    setLimitEnabled(true)
    setShowLimitForm(true)
  }

  function openEditLimit(limit: CategorySpendingLimit) {
    setEditingLimit(limit)
    setLimitCategoryId(limit.categoryId)
    setLimitAmount(String(limit.limitAmount))
    setLimitThresholds(normalizeThresholds(limit.thresholdPercents ?? []))
    setLimitAudience(
      limit.audience === 'Family' && !isPersonal ? 'Family' : 'Self',
    )
    setLimitEnabled(limit.isEnabled)
    setShowLimitForm(true)
  }

  function closeLimitForm() {
    setShowLimitForm(false)
    setEditingLimit(null)
  }

  async function handleLimitSubmit(event: FormEvent) {
    event.preventDefault()
    const amount = Number.parseFloat(limitAmount.replace(',', '.'))
    if (!limitCategoryId || Number.isNaN(amount) || amount <= 0) {
      showToast({
        title: 'Проверьте форму',
        message: 'Нужны категория и сумма лимита больше нуля.',
        tone: 'warning',
      })
      return
    }
    const thresholds = normalizeThresholds(limitThresholds)
    if (thresholds.length === 0) {
      showToast({
        title: 'Добавьте пороги',
        message: 'Нужен хотя бы один порог от 1 до 100%.',
        tone: 'warning',
      })
      return
    }

    const nextAudience: ReminderAudience =
      limitAudience === 'Family' && !isPersonal ? 'Family' : 'Self'

    try {
      if (editingLimit) {
        await updateLimit.mutateAsync({
          id: editingLimit.id,
          limitAmount: amount,
          thresholdPercents: thresholds,
          audience: nextAudience,
          isEnabled: limitEnabled,
        })
      } else {
        await createLimit.mutateAsync({
          categoryId: limitCategoryId,
          limitAmount: amount,
          thresholdPercents: thresholds,
          audience: nextAudience,
        })
      }
      closeLimitForm()
    } catch (err) {
      const message =
        err instanceof ApiError
          ? ((err.body as { message?: string } | null)?.message ?? err.message)
          : 'Не удалось сохранить лимит'
      showToast({ title: 'Ошибка', message, tone: 'warning' })
    }
  }

  async function handleSaveProfile(event: FormEvent) {
    event.preventDefault()
    const name = displayName.trim()
    if (!name) return
    await updateProfile.mutateAsync(name)
  }

  async function handleSaveBudget(event: FormEvent) {
    event.preventDefault()
    const day = Number.parseInt(periodStartDay, 10)
    if (day < 1 || day > 28) return
    await updateBudget.mutateAsync({ periodStartDay: day, baseCurrency })
  }

  async function handleAvatarChange(event: React.ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0]
    event.target.value = ''
    if (!file) return
    const maxBytes = 2 * 1024 * 1024
    if (file.size > maxBytes) {
      window.alert('Аватар не больше 2 МБ')
      return
    }
    if (!file.type.startsWith('image/')) {
      window.alert('Нужен файл изображения')
      return
    }
    try {
      await uploadAvatar.mutateAsync(file)
    } catch {
      window.alert('Не удалось загрузить аватар')
    }
  }

  if (isLoading || (activeTab === 'categories' && limitsLoading)) {
    return (
      <div className="flex justify-center py-20">
        <Spinner />
      </div>
    )
  }

  if (isError || !settings) {
    return (
      <EmptyState
        title="Не удалось загрузить настройки"
        action={
          <Button variant="secondary" onClick={() => void refetch()}>
            Повторить
          </Button>
        }
      />
    )
  }

  const canEditBudget = settings.canManageFamilySettings ?? false
  const canManagePlan = settings.canManagePlan ?? true
  const usedCategoryIds = new Set((categoryLimits ?? []).map((l) => l.categoryId))
  const availableLimitCategories = expenseCategories.filter(
    (c) => !usedCategoryIds.has(c.id) || c.id === editingLimit?.categoryId,
  )

  return (
    <div className="space-y-6">
      <PageHeader title="Настройки" subtitle="Профиль, бюджет, категории и лимиты" />

      <Tabs
        tabs={[
          { id: 'general', label: 'Общие' },
          { id: 'categories', label: 'Категории и лимиты' },
        ]}
        activeTab={activeTab}
        onChange={setTab}
      />

      {activeTab === 'general' && (
        <>
          <Card>
            <CardTitle>Профиль</CardTitle>
            <form className="mt-4 space-y-4" onSubmit={(e) => void handleSaveProfile(e)}>
              <div className="flex items-center gap-4">
                {user?.avatarUrl ? (
                  <img
                    src={user.avatarUrl}
                    alt=""
                    className="size-16 rounded-full object-cover ring-2 ring-slate-200"
                  />
                ) : (
                  <div className="flex size-16 items-center justify-center rounded-full bg-slate-100 text-xl font-bold text-slate-500">
                    {(user?.displayName ?? '?').slice(0, 1).toUpperCase()}
                  </div>
                )}
                <label className="cursor-pointer text-sm font-medium text-brand-600 hover:text-brand-700">
                  Загрузить аватар
                  <input
                    type="file"
                    accept="image/jpeg,image/png,image/webp,image/gif"
                    className="hidden"
                    onChange={(e) => void handleAvatarChange(e)}
                  />
                </label>
              </div>
              <p className="text-xs text-slate-500">JPEG, PNG, WebP или GIF · до 2 МБ</p>
              <Input
                label="Отображаемое имя"
                value={displayName}
                onChange={(e) => setDisplayName(e.target.value)}
              />
              {user?.username && (
                <p className="text-sm text-slate-500">Telegram: @{user.username}</p>
              )}
              <Button type="submit" loading={updateProfile.isPending || uploadAvatar.isPending}>
                Сохранить профиль
              </Button>
            </form>
          </Card>

          {(contexts?.length ?? 0) > 1 && (
            <Card>
              <CardTitle>Активный бюджет</CardTitle>
              <CardDescription>Переключение между личным и семейным</CardDescription>
              <ul className="mt-4 space-y-2">
                {contexts?.map((ctx) => (
                  <li key={ctx.id}>
                    <Button
                      variant={user?.activeContextId === ctx.id ? 'primary' : 'secondary'}
                      className="w-full justify-start"
                      loading={switchContext.isPending}
                      onClick={() => void switchContext.mutateAsync(ctx.id)}
                    >
                      {ctx.name} {ctx.isPersonal ? '(личный)' : '(семья)'}
                    </Button>
                  </li>
                ))}
              </ul>
            </Card>
          )}

          <Card>
            <CardTitle>Бюджет</CardTitle>
            <CardDescription>Текущий контекст и период</CardDescription>
            <dl className="mt-4 space-y-3 text-sm">
              <div className="flex justify-between gap-4">
                <dt className="text-slate-500">Название</dt>
                <dd className="font-medium text-slate-900">{settings.contextName}</dd>
              </div>
              <div className="flex justify-between gap-4">
                <dt className="text-slate-500">Тип</dt>
                <dd className="font-medium text-slate-900">
                  {settings.isPersonal ? 'Личный' : 'Семейный'}
                </dd>
              </div>
            </dl>

            {canEditBudget ? (
              <form className="mt-4 grid gap-3 sm:grid-cols-2" onSubmit={(e) => void handleSaveBudget(e)}>
                <Input
                  label="Начало периода (число)"
                  value={periodStartDay}
                  onChange={(e) => setPeriodStartDay(e.target.value)}
                />
                <Select
                  label="Базовая валюта"
                  value={baseCurrency}
                  onChange={(e) => setBaseCurrency(e.target.value)}
                  options={currencyOptions}
                />
                <Button type="submit" loading={updateBudget.isPending} className="sm:col-span-2 sm:w-auto">
                  Сохранить параметры
                </Button>
              </form>
            ) : (
              <dl className="mt-4 space-y-3 text-sm">
                <div className="flex justify-between gap-4">
                  <dt className="text-slate-500">Валюта</dt>
                  <dd className="font-medium text-slate-900">{settings.baseCurrency}</dd>
                </div>
                <div className="flex justify-between gap-4">
                  <dt className="text-slate-500">Начало периода</dt>
                  <dd className="font-medium text-slate-900">{settings.periodStartDay}-е число</dd>
                </div>
              </dl>
            )}
          </Card>

          <MobileMoreMenu />

          <Button
            variant="danger"
            className="w-full sm:w-auto"
            loading={logout.isPending}
            onClick={() => void handleLogout()}
          >
            Выйти
          </Button>
        </>
      )}

      {activeTab === 'categories' && (
        <>
          {!canManagePlan ? (
            <EmptyState title="Недостаточно прав для управления категориями" />
          ) : (
            <>
              <Card>
                <CardTitle>Категории</CardTitle>
                <CardDescription>
                  Добавляйте, переименовывайте и меняйте порядок перетаскиванием
                </CardDescription>

                <form
                  className="mt-4 flex flex-col gap-3 sm:flex-row sm:items-end"
                  onSubmit={(e) => void handleAddCategory(e)}
                >
                  <Input
                    label="Новая категория"
                    value={newCategoryName}
                    onChange={(e) => setNewCategoryName(e.target.value)}
                    placeholder="Продукты"
                  />
                  <Button type="submit" loading={createCategory.isPending} className="shrink-0">
                    Добавить
                  </Button>
                </form>

                {categories.length === 0 ? (
                  <p className="mt-4 text-sm text-slate-500">Категорий пока нет — создайте свои.</p>
                ) : (
                  <>
                    <div className="mt-4 flex flex-wrap items-center justify-between gap-3">
                      <label className="flex cursor-pointer items-center gap-2 text-sm text-slate-700">
                        <input
                          type="checkbox"
                          className="size-4 rounded border-slate-300"
                          checked={
                            categories.length > 0 &&
                            categories.every((c) => selectedCategoryIds.has(c.id))
                          }
                          onChange={() => toggleAllCategories(categories.map((c) => c.id))}
                        />
                        Выбрать все
                        {selectedCategoryIds.size > 0 && (
                          <span className="text-slate-500">({selectedCategoryIds.size})</span>
                        )}
                      </label>
                      {selectedCategoryIds.size > 0 && (
                        <Button
                          size="sm"
                          variant="danger"
                          loading={deleteCategories.isPending}
                          onClick={() => void handleBulkDeleteCategories()}
                        >
                          Удалить выбранные
                        </Button>
                      )}
                    </div>

                    <SortableCategoryList
                      categories={categories.map((c) => ({ id: c.id, name: c.name }))}
                      selectedIds={selectedCategoryIds}
                      disabled={reorderCategories.isPending}
                      editingId={editingId}
                      editingName={editingName}
                      onEditingNameChange={setEditingName}
                      onToggleSelect={toggleCategory}
                      onStartEdit={(id, name) => {
                        setEditingId(id)
                        setEditingName(name)
                      }}
                      onCancelEdit={() => setEditingId(null)}
                      onSaveEdit={(id) => void handleSaveCategory(id)}
                      onDelete={(id, name) => {
                        void (async () => {
                          const accepted = await confirm({
                            title: `Удалить категорию «${name}»?`,
                            message: 'У связанных операций категория будет очищена.',
                          })
                          if (accepted) {
                            void deleteCategory.mutateAsync(id).then(() => {
                              setSelectedCategoryIds((prev) => {
                                const next = new Set(prev)
                                next.delete(id)
                                return next
                              })
                            })
                          }
                        })()
                      }}
                      onReorder={(orderedIds) => void handleReorderCategories(orderedIds)}
                      savePending={updateCategory.isPending}
                      deletePending={deleteCategory.isPending}
                    />
                  </>
                )}
              </Card>

              <Card>
                <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
                  <div>
                    <CardTitle>Лимиты и уведомления</CardTitle>
                    <CardDescription>
                      Сумма на текущий бюджетный период и пороги %. После удаления расхода пороги
                      снова могут сработать, если лимит снова превышен.
                    </CardDescription>
                  </div>
                  {!showLimitForm && (
                    <Button
                      variant="secondary"
                      onClick={openCreateLimit}
                      disabled={availableLimitCategories.length === 0 && !editingLimit}
                    >
                      Добавить лимит
                    </Button>
                  )}
                </div>

                {showLimitForm && (
                  <form
                    className="mt-4 space-y-4 border-t border-slate-100 pt-4"
                    onSubmit={(e) => void handleLimitSubmit(e)}
                  >
                    {!editingLimit && (
                      <Select
                        label="Категория"
                        value={limitCategoryId}
                        onChange={(e) => setLimitCategoryId(e.target.value)}
                        options={availableLimitCategories.map((c) => ({
                          value: c.id,
                          label: c.name,
                        }))}
                        required
                      />
                    )}
                    {editingLimit && (
                      <p className="text-sm font-medium text-slate-800">
                        {editingLimit.categoryName}
                      </p>
                    )}
                    <Input
                      label={`Лимит (${settings.baseCurrency})`}
                      type="number"
                      min={0.01}
                      step="0.01"
                      value={limitAmount}
                      onChange={(e) => setLimitAmount(e.target.value)}
                      required
                    />
                    <ThresholdEditor values={limitThresholds} onChange={setLimitThresholds} />
                    {!isPersonal && (
                      <Select
                        label="Кому уведомление"
                        value={limitAudience}
                        onChange={(e) =>
                          setLimitAudience(e.target.value as ReminderAudience)
                        }
                        options={audienceOptions}
                      />
                    )}
                    {editingLimit && (
                      <label className="flex items-center gap-2 text-sm text-slate-700">
                        <input
                          type="checkbox"
                          checked={limitEnabled}
                          onChange={(e) => setLimitEnabled(e.target.checked)}
                          className="size-4 rounded border-slate-300"
                        />
                        Уведомления включены
                      </label>
                    )}
                    <div className="flex flex-wrap gap-2">
                      <Button
                        type="submit"
                        loading={createLimit.isPending || updateLimit.isPending}
                      >
                        {editingLimit ? 'Сохранить' : 'Создать'}
                      </Button>
                      <Button type="button" variant="secondary" onClick={closeLimitForm}>
                        Отмена
                      </Button>
                    </div>
                  </form>
                )}

                {limitProgressRows.length > 0 && (
                  <div className="mt-4 rounded-xl border border-slate-100 bg-slate-50/80 px-4 py-3">
                    <p className="text-sm font-medium text-slate-800">Прогресс за период</p>
                    <CategorySpendProgressList
                      rows={limitProgressRows}
                      currency={settings.baseCurrency}
                    />
                  </div>
                )}

                <ul className="mt-4 divide-y divide-slate-100">
                  {(categoryLimits ?? []).length === 0 && !showLimitForm ? (
                    <li className="py-2">
                      <EmptyState
                        title="Лимитов пока нет"
                        description="Задайте лимит на категорию и пороги, например 50% и 80%."
                        action={
                          <Button onClick={openCreateLimit}>Добавить лимит</Button>
                        }
                      />
                    </li>
                  ) : (
                    (categoryLimits ?? []).map((limit) => (
                      <li
                        key={limit.id}
                        className="flex flex-col gap-3 py-4 first:pt-0 last:pb-0 sm:flex-row sm:items-center sm:justify-between"
                      >
                        <div className="min-w-0 space-y-1">
                          <div className="flex flex-wrap items-center gap-2">
                            <p className="font-medium text-slate-900">{limit.categoryName}</p>
                            {!limit.isEnabled && <Badge>Выкл</Badge>}
                            {limit.audience === 'Family' && <Badge>Семья</Badge>}
                          </div>
                          <p className="text-sm text-slate-500">
                            {limit.limitAmount} {settings.baseCurrency} · пороги{' '}
                            {(limit.thresholdPercents ?? []).map((t) => `${t}%`).join(', ') ||
                              '—'}
                          </p>
                        </div>
                        {limit.canEdit && (
                          <div className="flex shrink-0 flex-wrap gap-2">
                            <Button
                              variant="secondary"
                              size="sm"
                              onClick={() => openEditLimit(limit)}
                            >
                              Изменить
                            </Button>
                            <Button
                              variant="ghost"
                              size="sm"
                              className="text-red-600"
                              loading={deleteLimit.isPending}
                              onClick={async () => {
                                const accepted = await confirm({
                                  title: 'Удалить лимит?',
                                  message: `Лимит для «${limit.categoryName}» будет удалён.`,
                                })
                                if (accepted) void deleteLimit.mutateAsync(limit.id)
                              }}
                            >
                              Удалить
                            </Button>
                          </div>
                        )}
                      </li>
                    ))
                  )}
                </ul>
              </Card>
            </>
          )}
        </>
      )}
    </div>
  )
}
