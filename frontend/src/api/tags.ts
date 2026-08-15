import { apiFetch } from './client'
import type { Tag } from './types'

export const listTags = (signal?: AbortSignal) => apiFetch<Tag[]>('/api/tags', { signal })
export const createTag = (name: string) =>
  apiFetch<Tag>('/api/tags', { method: 'POST', body: JSON.stringify({ name }) })
export const renameTag = (tagId: string, name: string) =>
  apiFetch<Tag>(`/api/tags/${tagId}`, { method: 'PUT', body: JSON.stringify({ name }) })
