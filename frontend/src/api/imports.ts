import { apiFetch } from './client'
import type { ImportResult } from './types'
export const uploadStatement = (file: File, password: string | undefined, signal: AbortSignal) => { const body = new FormData(); body.append('file', file); if (password) body.append('password', password); return apiFetch<ImportResult>('/api/document-extractions', { method: 'POST', body, signal }) }
