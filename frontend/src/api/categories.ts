import { apiFetch } from './client'
import type { Category } from './types'
let cache: Promise<Category[]> | undefined
export function getCategories(): Promise<Category[]> { return cache ??= apiFetch<Category[]>('/api/categories').catch(error => { cache = undefined; throw error }) }
