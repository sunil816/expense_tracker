import { apiFetch } from './client'
import type { SpendingReport } from './types'
export const getSpendingReport = (granularity: 'week' | 'month', from?: string, to?: string, signal?: AbortSignal) => { const search = new URLSearchParams({ granularity }); if (from) search.set('from', from); if (to) search.set('to', to); return apiFetch<SpendingReport>(`/api/reports/spending?${search}`, { signal }) }
