export class ApiError extends Error {
  constructor(public status: number, public title: string, public detail?: string, public errors?: Record<string, string[]>) { super(title); this.name = 'ApiError' }
}
const camel = (key: string) => key.length ? key[0].toLowerCase() + key.slice(1) : key
export async function apiFetch<T>(path: string, init: RequestInit = {}): Promise<T> {
  const headers = new Headers(init.headers)
  if (typeof init.body === 'string') headers.set('content-type', 'application/json')
  try {
    const response = await fetch(path, { ...init, headers })
    if (response.ok) return await response.json() as T
    const contentType = response.headers.get('content-type') ?? ''
    if (contentType.includes('application/problem+json')) {
      const body = await response.json() as { title?: string; detail?: string; errors?: Record<string, string[]> }
      const errors = body.errors && Object.fromEntries(Object.entries(body.errors).map(([key, value]) => [camel(key), value]))
      throw new ApiError(response.status, body.title ?? 'Request failed', body.detail, errors)
    }
    throw new ApiError(response.status, 'Request failed')
  } catch (error) {
    if (error instanceof ApiError || (error instanceof DOMException && error.name === 'AbortError')) throw error
    if (error instanceof TypeError) throw new ApiError(0, 'Network error')
    throw new ApiError(0, 'Network error')
  }
}
export async function apiFetchVoid(path: string, init?: RequestInit): Promise<void> { await apiFetch<unknown>(path, init) }
