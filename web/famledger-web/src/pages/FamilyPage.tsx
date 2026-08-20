import { useState } from 'react'
import {
  useApproveJoinRequest,
  useCreateFamily,
  useFamily,
  useJoinFamily,
  useMe,
  useRegenerateInviteCode,
  useRejectJoinRequest,
  useUpdateMemberRole,
} from '../api/hooks'
import type { FamilyMemberRole } from '../api/types'
import { Card, CardDescription, CardTitle } from '../components/ui/Card'
import { Button } from '../components/ui/Button'
import { Select } from '../components/ui/Input'
import { EmptyState, PageHeader, Spinner, Badge, Tabs } from '../components/ui/Tabs'
import { formatDateTime, roleLabel } from '../lib/format'
import { copyToClipboard } from '../lib/clipboard'

export function FamilyPage() {
  const { data: family, isLoading, isError, refetch } = useFamily()
  const { data: user } = useMe()
  const createFamily = useCreateFamily()
  const joinFamily = useJoinFamily()
  const approve = useApproveJoinRequest()
  const reject = useRejectJoinRequest()
  const updateRole = useUpdateMemberRole()
  const regenerateInvite = useRegenerateInviteCode()
  const [copied, setCopied] = useState(false)
  const [familyName, setFamilyName] = useState('')
  const [inviteCode, setInviteCode] = useState('')
  const [joinMessage, setJoinMessage] = useState<string | null>(null)
  const [createError, setCreateError] = useState<string | null>(null)
  const [activeTab, setActiveTab] = useState('members')
  const [approveRoles, setApproveRoles] = useState<Record<string, FamilyMemberRole>>({})

  async function copyInviteCode() {
    if (!family?.inviteCode) return
    const ok = await copyToClipboard(family.inviteCode)
    if (!ok) return
    setCopied(true)
    setTimeout(() => setCopied(false), 2000)
  }

  async function handleCreateFamily(e: React.FormEvent) {
    e.preventDefault()
    const name = familyName.trim()
    if (!name) return
    setCreateError(null)
    try {
      await createFamily.mutateAsync(name)
      setFamilyName('')
    } catch {
      setCreateError('Не удалось создать семью')
    }
  }

  async function handleJoinFamily(e: React.FormEvent) {
    e.preventDefault()
    const code = inviteCode.trim()
    if (!code) return
    setJoinMessage(null)
    try {
      await joinFamily.mutateAsync(code)
      setInviteCode('')
      setJoinMessage('Запрос отправлен. Дождитесь одобрения от главы семьи.')
    } catch {
      setJoinMessage('Не удалось отправить запрос. Проверьте код.')
    }
  }

  if (isLoading) {
    return (
      <div className="flex justify-center py-20">
        <Spinner />
      </div>
    )
  }

  if (isError || !family) {
    return (
      <EmptyState
        title="Не удалось загрузить данные семьи"
        action={
          <Button variant="secondary" onClick={() => void refetch()}>
            Повторить
          </Button>
        }
      />
    )
  }

  if (family.isPersonal) {
    return (
      <div className="space-y-6">
        <PageHeader
          title="Семья"
          subtitle="Сейчас активен личный бюджет"
        />

        <Card>
          <CardTitle>Создать семейный бюджет</CardTitle>
          <CardDescription>
            Объедините бюджет с близкими: общие расходы, роли и приглашения по коду.
          </CardDescription>
          <form className="mt-4 space-y-3" onSubmit={(e) => void handleCreateFamily(e)}>
            <input
              type="text"
              value={familyName}
              onChange={(e) => setFamilyName(e.target.value)}
              placeholder="Название семьи, например: Семья Ивановых"
              className="w-full rounded-xl border border-slate-200 px-4 py-2.5 text-sm outline-none focus:border-brand-500 focus:ring-2 focus:ring-brand-500/20"
              disabled={createFamily.isPending}
            />
            <Button type="submit" loading={createFamily.isPending} disabled={!familyName.trim()}>
              Создать семью
            </Button>
            {createError && (
              <p className="text-sm text-red-600">{createError}</p>
            )}
          </form>
        </Card>

        <Card>
          <CardTitle>Присоединиться по коду</CardTitle>
          <CardDescription>
            Если вас пригласили, введите код приглашения от главы семьи.
          </CardDescription>
          <form className="mt-4 space-y-3" onSubmit={(e) => void handleJoinFamily(e)}>
            <input
              type="text"
              value={inviteCode}
              onChange={(e) => setInviteCode(e.target.value)}
              placeholder="Код приглашения"
              className="w-full rounded-xl border border-slate-200 px-4 py-2.5 font-mono text-sm tracking-widest outline-none focus:border-brand-500 focus:ring-2 focus:ring-brand-500/20"
              disabled={joinFamily.isPending}
            />
            <Button
              type="submit"
              variant="secondary"
              loading={joinFamily.isPending}
              disabled={!inviteCode.trim()}
            >
              Отправить запрос
            </Button>
            {joinMessage && (
              <p className="text-sm text-slate-600">{joinMessage}</p>
            )}
          </form>
        </Card>
      </div>
    )
  }

  const pendingRequests = family.joinRequests.filter((r) => r.status === 'Pending')
  const canManageRequests =
    family.myRole === 'Head' || family.myRole === 'Assistant'
  const isHead = family.myRole === 'Head'

  const roleOptionsForApprove = [
    { value: 'Member', label: 'Участник' },
    { value: 'Assistant', label: 'Помощник' },
    ...(isHead ? [{ value: 'Head', label: 'Глава' }] : []),
  ]

  const tabs = [
    { id: 'members', label: 'Участники' },
    { id: 'invite', label: 'Пригласить' },
    {
      id: 'requests',
      label: pendingRequests.length > 0 ? `Запросы (${pendingRequests.length})` : 'Запросы',
    },
  ]

  async function handleRoleChange(memberId: string, role: FamilyMemberRole) {
    if (!user?.activeContextId) return
    await updateRole.mutateAsync({
      contextId: user.activeContextId,
      memberId,
      role,
    })
  }

  async function handleRegenerate() {
    if (!user?.activeContextId) return
    await regenerateInvite.mutateAsync(user.activeContextId)
    setCopied(false)
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title="Семья"
        subtitle={family.contextName ?? 'Семейный бюджет'}
      />

      <Tabs tabs={tabs} activeTab={activeTab} onChange={setActiveTab} />

      {activeTab === 'members' && (
        <section className="space-y-3">
          {family.members.length === 0 ? (
            <EmptyState title="Участников пока нет" />
          ) : (
            <Card padding="none">
              <ul className="divide-y divide-slate-100">
                {family.members.map((member) => (
                  <li key={member.id} className="flex items-center justify-between gap-4 px-5 py-4">
                    <div>
                      <p className="font-medium text-slate-900">{member.displayName}</p>
                      {member.username && (
                        <p className="text-sm text-slate-500">@{member.username}</p>
                      )}
                    </div>
                    {isHead && member.userId !== user?.id ? (
                      <Select
                        value={member.role}
                        onChange={(e) =>
                          void handleRoleChange(member.id, e.target.value as FamilyMemberRole)
                        }
                        options={[
                          { value: 'Head', label: 'Глава' },
                          { value: 'Assistant', label: 'Помощник' },
                          { value: 'Member', label: 'Участник' },
                        ]}
                      />
                    ) : (
                      <Badge>{roleLabel(member.role)}</Badge>
                    )}
                  </li>
                ))}
              </ul>
            </Card>
          )}
        </section>
      )}

      {activeTab === 'invite' && (
        <Card>
          <CardTitle>Пригласить в семью</CardTitle>
          <CardDescription>
            Отправьте код близкому. После заявки выберите роль на вкладке «Запросы».
          </CardDescription>
          <div className="mt-4 flex flex-wrap items-center gap-3">
            <code className="rounded-xl bg-slate-100 px-4 py-2.5 font-mono text-lg font-bold tracking-widest text-slate-900">
              {family.inviteCode || '———'}
            </code>
            <Button variant="secondary" onClick={() => void copyInviteCode()}>
              {copied ? 'Скопировано' : 'Копировать'}
            </Button>
            {isHead && (
              <Button
                variant="ghost"
                loading={regenerateInvite.isPending}
                onClick={() => void handleRegenerate()}
              >
                Обновить код
              </Button>
            )}
          </div>
        </Card>
      )}

      {activeTab === 'requests' && (
        <section className="space-y-3">
          {pendingRequests.length === 0 ? (
            <EmptyState
              title="Нет ожидающих запросов"
              description="Когда кто-то отправит запрос по коду, он появится здесь."
            />
          ) : !canManageRequests ? (
            <p className="text-sm text-slate-500">
              Есть {pendingRequests.length} запрос(ов). Одобрить может глава или помощник.
            </p>
          ) : (
            <Card padding="none">
              <ul className="divide-y divide-slate-100">
                {pendingRequests.map((request) => {
                  const role = approveRoles[request.id] ?? 'Member'
                  return (
                    <li key={request.id} className="px-5 py-4">
                      <div className="flex flex-wrap items-end justify-between gap-3">
                        <div className="min-w-0 flex-1">
                          <p className="font-medium text-slate-900">{request.displayName}</p>
                          {request.username && (
                            <p className="text-sm text-slate-500">@{request.username}</p>
                          )}
                          <p className="mt-1 text-xs text-slate-400">
                            {formatDateTime(request.createdAt)}
                          </p>
                          <div className="mt-3 max-w-xs">
                            <Select
                              label="Роль при принятии"
                              value={role}
                              onChange={(e) =>
                                setApproveRoles((prev) => ({
                                  ...prev,
                                  [request.id]: e.target.value as FamilyMemberRole,
                                }))
                              }
                              options={roleOptionsForApprove}
                            />
                          </div>
                        </div>
                        <div className="flex gap-2">
                          <Button
                            size="sm"
                            loading={approve.isPending}
                            onClick={() =>
                              void approve.mutateAsync({ requestId: request.id, role })
                            }
                          >
                            Принять
                          </Button>
                          <Button
                            size="sm"
                            variant="secondary"
                            loading={reject.isPending}
                            onClick={() => void reject.mutateAsync(request.id)}
                          >
                            Отклонить
                          </Button>
                        </div>
                      </div>
                    </li>
                  )
                })}
              </ul>
            </Card>
          )}
        </section>
      )}
    </div>
  )
}
