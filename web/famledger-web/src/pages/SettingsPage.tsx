import { useEffect, useState, type FormEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import {
  useContexts,
  useCreateCategory,
  useDeleteCategories,
  useDeleteCategory,
  useLogout,
  useMe,
  useSettings,
  useSwitchContext,
  useUpdateBudgetSettings,
  useUpdateCategory,
  useUpdateProfile,
  useUploadAvatar,
} from '../api/hooks'
import { currencyOptions } from '../api/types'
import { Card, CardDescription, CardTitle } from '../components/ui/Card'
import { Button } from '../components/ui/Button'
import { useConfirmDialog } from '../components/ui/ConfirmDialog'
import { Input, Select } from '../components/ui/Input'
import { EmptyState, PageHeader, Spinner } from '../components/ui/Tabs'
import { MobileMoreMenu } from '../components/layout/Navigation'

export function SettingsPage() {
  const navigate = useNavigate()
  const { confirm } = useConfirmDialog()
  const { data: user } = useMe()
  const { data: settings, isLoading, isError, refetch } = useSettings()
  const { data: contexts } = useContexts()
  const logout = useLogout()
  const createCategory = useCreateCategory()
  const updateCategory = useUpdateCategory()
  const deleteCategory = useDeleteCategory()
  const deleteCategories = useDeleteCategories()
  const updateProfile = useUpdateProfile()
  const uploadAvatar = useUploadAvatar()
  const updateBudget = useUpdateBudgetSettings()
  const switchContext = useSwitchContext()

  const [displayName, setDisplayName] = useState('')
  const [periodStartDay, setPeriodStartDay] = useState('15')
  const [baseCurrency, setBaseCurrency] = useState('RSD')
  const [newCategoryName, setNewCategoryName] = useState('')
  const [editingId, setEditingId] = useState<string | null>(null)
  const [editingName, setEditingName] = useState('')
  const [selectedCategoryIds, setSelectedCategoryIds] = useState<Set<string>>(new Set())

  useEffect(() => {
    if (user?.displayName) setDisplayName(user.displayName)
  }, [user?.displayName])

  useEffect(() => {
    if (settings) {
      setPeriodStartDay(String(settings.periodStartDay))
      setBaseCurrency(settings.baseCurrency)
    }
  }, [settings])

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
    if (!file) return
    await uploadAvatar.mutateAsync(file)
  }

  if (isLoading) {
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

  const categories = settings.categories ?? []
  const canEditBudget = settings.canManageFamilySettings ?? false
  const canManagePlan = settings.canManagePlan ?? true

  return (
    <div className="space-y-6">
      <PageHeader title="Настройки" subtitle="Профиль и параметры бюджета" />

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
              <input type="file" accept="image/*" className="hidden" onChange={(e) => void handleAvatarChange(e)} />
            </label>
          </div>
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

      {canManagePlan && (
        <Card>
          <CardTitle>Категории расходов</CardTitle>
          <CardDescription>Добавляйте, переименовывайте и удаляйте категории</CardDescription>

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

              <ul className="mt-3 divide-y divide-slate-100 rounded-xl border border-slate-200">
                {categories.map((cat) => (
                  <li key={cat.id} className="flex items-center justify-between gap-3 px-4 py-3">
                    {editingId === cat.id ? (
                      <div className="flex flex-1 items-center gap-2">
                        <input
                          className="flex-1 rounded-lg border border-slate-200 px-3 py-1.5 text-sm"
                          value={editingName}
                          onChange={(e) => setEditingName(e.target.value)}
                        />
                        <Button size="sm" loading={updateCategory.isPending} onClick={() => void handleSaveCategory(cat.id)}>
                          Сохранить
                        </Button>
                        <Button size="sm" variant="ghost" onClick={() => setEditingId(null)}>
                          Отмена
                        </Button>
                      </div>
                    ) : (
                      <>
                        <label className="flex min-w-0 flex-1 cursor-pointer items-center gap-3">
                          <input
                            type="checkbox"
                            className="size-4 shrink-0 rounded border-slate-300"
                            checked={selectedCategoryIds.has(cat.id)}
                            onChange={() => toggleCategory(cat.id)}
                          />
                          <span className="truncate font-medium text-slate-900">{cat.name}</span>
                        </label>
                        <div className="flex shrink-0 gap-1">
                          <Button
                            size="sm"
                            variant="ghost"
                            onClick={() => {
                              setEditingId(cat.id)
                              setEditingName(cat.name)
                            }}
                          >
                            Изменить
                          </Button>
                          <Button
                            size="sm"
                            variant="ghost"
                            className="text-red-600 hover:bg-red-50"
                            loading={deleteCategory.isPending}
                            onClick={async () => {
                              const accepted = await confirm({
                                title: `Удалить категорию «${cat.name}»?`,
                                message: 'У связанных операций категория будет очищена.',
                              })
                              if (accepted) {
                                void deleteCategory.mutateAsync(cat.id).then(() => {
                                  setSelectedCategoryIds((prev) => {
                                    const next = new Set(prev)
                                    next.delete(cat.id)
                                    return next
                                  })
                                })
                              }
                            }}
                          >
                            Удалить
                          </Button>
                        </div>
                      </>
                    )}
                  </li>
                ))}
              </ul>
            </>
          )}
        </Card>
      )}

      <MobileMoreMenu />

      <Button
        variant="danger"
        className="w-full sm:w-auto"
        loading={logout.isPending}
        onClick={() => void handleLogout()}
      >
        Выйти
      </Button>
    </div>
  )
}
