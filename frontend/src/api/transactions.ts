import { apiFetch } from './client'
import type { BulkCategoryResponse, DuplicateFlagDecision, DuplicateFlagDetails, TransactionDetail, TransactionListParams, TransactionPage, TransactionSummary, TransactionWrite, ManualTransactionCreated } from './types'
const query = (params: TransactionListParams) => { const search = new URLSearchParams(); Object.entries(params).forEach(([key, value]) => value !== undefined && value !== '' && search.set(key, String(value))); return search.toString() }
export const listTransactions = (params: TransactionListParams, signal?: AbortSignal) => apiFetch<TransactionPage>(`/api/transactions?${query(params)}`, { signal })
export const getTransaction = (id: string, signal?: AbortSignal) => apiFetch<TransactionDetail>(`/api/transactions/${id}`, { signal })
export const createTransaction = (body: TransactionWrite) => apiFetch<ManualTransactionCreated>('/api/transactions', { method: 'POST', body: JSON.stringify(body) })
export const updateTransaction = (id: string, body: TransactionWrite) => apiFetch<TransactionDetail>(`/api/transactions/${id}`, { method: 'PUT', body: JSON.stringify(body) })
export const findMatchingTransactions = (description: string, excludeId: string) => apiFetch<TransactionSummary[]>(`/api/transactions/matches?description=${encodeURIComponent(description)}&excludeId=${excludeId}`)
export const updateCategoryForMatches = (description: string, categoryId: string | null) => apiFetch<BulkCategoryResponse>('/api/transactions/category/matches', { method: 'PUT', body: JSON.stringify({ description, categoryId }) })
export const addTransactionTag = (transactionId: string, tagId: string) => apiFetch<TransactionDetail>(`/api/transactions/${transactionId}/tags`, { method: 'POST', body: JSON.stringify({ tagId }) })
export const removeTransactionTag = (transactionId: string, tagId: string) => apiFetch<TransactionDetail>(`/api/transactions/${transactionId}/tags/${tagId}`, { method: 'DELETE' })
export const decideDuplicateFlag = (transactionId: string, state: DuplicateFlagDecision, signal?: AbortSignal) => apiFetch<DuplicateFlagDetails>(`/api/transactions/${transactionId}/duplicate-flag`, { method: 'PUT', body: JSON.stringify({ state }), signal })
