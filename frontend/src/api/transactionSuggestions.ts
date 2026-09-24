import { apiFetch } from './client'
import type { SuggestionDecision, SuggestionState, TransactionKind, TransactionSuggestion, TransactionSuggestionPage } from './types'

export const listTransactionSuggestions = (params: { state?: SuggestionState; page?: number; pageSize?: number }, signal?: AbortSignal) => {
  const query = new URLSearchParams()
  if (params.state) query.set('state', params.state)
  if (params.page) query.set('page', String(params.page))
  if (params.pageSize) query.set('pageSize', String(params.pageSize))
  return apiFetch<TransactionSuggestionPage>(`/api/transaction-suggestions?${query}`, { signal })
}

export const decideTransactionSuggestion = (transactionId: string, suggestionId: string, state: SuggestionDecision, kind?: TransactionKind, signal?: AbortSignal) =>
  apiFetch<TransactionSuggestion>(`/api/transactions/${transactionId}/suggestions/${suggestionId}`, { method: 'PUT', body: JSON.stringify({ state, kind }), signal })

export const bulkDecideTransactionSuggestions = (suggestionIds: string[], state: 'Confirmed' | 'Rejected', signal?: AbortSignal) =>
  apiFetch<{ updatedCount: number }>('/api/transaction-suggestions/bulk-decision', { method: 'POST', body: JSON.stringify({ suggestionIds, state }), signal })