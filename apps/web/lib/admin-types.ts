export type AdminUser = {
  id: string;
  name: string;
  email: string;
  createdAt: string;
  planCode?: string;
  planName?: string;
  subscriptionStatus?: string;
  licenseId?: string;
  accessStatus: string;
  accessExpiresAt?: string;
  accessSource?: string;
  deviceFingerprint?: string;
  deviceLastRenewedAt?: string;
};
export type AdminUserList = { items: AdminUser[]; nextCursor?: string };
export type AdminSubscription = {
  id: string;
  librarySlug: string;
  libraryName: string;
  planCode: string;
  planName: string;
  status: string;
  billingType: string;
  paidThrough?: string;
  createdAt: string;
  isCurrent: boolean;
};
export type AdminLicense = {
  id: string;
  generation: number;
  createdAt: string;
  revokedAt?: string;
  manualValidUntil?: string;
  manualGrantReason?: string;
  accessStatus: string;
  accessSource?: string;
  accessExpiresAt?: string;
};
export type AdminActivation = {
  id: string;
  deviceFingerprint: string;
  activatedAt: string;
  lastRenewedAt: string;
  revokedAt?: string;
  isCurrent: boolean;
};
export type AdminAudit = {
  id: string;
  adminEmail: string;
  action: string;
  targetType: string;
  targetId: string;
  reason: string;
  outcome: string;
  ipAddress?: string;
  createdAt: string;
};
export type AdminUserDetail = {
  id: string;
  name: string;
  email: string;
  createdAt: string;
  subscriptions: AdminSubscription[];
  license?: AdminLicense;
  activations: AdminActivation[];
  audit: AdminAudit[];
};

export const statusLabels: Record<string, string> = {
  active: 'Ativo',
  expired: 'Vencido',
  revoked: 'Revogado',
  none: 'Sem licença',
  pending_payment: 'Aguardando pagamento',
  past_due: 'Pagamento atrasado',
  suspended: 'Suspensa',
  provisioning: 'Preparando',
  provisioning_failed: 'Falha no provisionamento',
  pending_checkout: 'Aguardando checkout',
  checkout_cancelled: 'Checkout cancelado',
  checkout_expired: 'Checkout expirado',
  cancelled: 'Cancelada',
  replaced: 'Substituída',
  succeeded: 'Concluída',
  failed: 'Falhou',
};
export const sourceLabels: Record<string, string> = {
  paid: 'Pagamento',
  manual: 'Concessão manual',
  paid_and_manual: 'Pagamento + manual',
};
export function formatDate(value?: string) {
  return value
    ? new Intl.DateTimeFormat('pt-BR', { dateStyle: 'short', timeStyle: 'short' }).format(
        new Date(value),
      )
    : '—';
}
