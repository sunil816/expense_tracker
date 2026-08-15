import { apiFetch } from './client'
import type { DuplicateReviewPage, DuplicateReviewQuery } from './types'

export const listDuplicateReviews = (params: DuplicateReviewQuery, signal?: AbortSignal) => {
  const query = new URLSearchParams()
  if (params.state) query.set('state', params.state)
  query.set('page', String(params.page ?? 1))
  query.set('pageSize', String(params.pageSize ?? 25))
  return apiFetch<DuplicateReviewPage>(`/api/duplicate-flags?${query}`, { signal })
}