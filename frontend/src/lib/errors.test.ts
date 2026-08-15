import { describe, expect, it } from 'vitest'
import { ApiError } from '../api/client'
import { uploadErrorMessage } from './errors'
describe('upload errors', () => { it('maps infrastructure and document errors', () => { expect(uploadErrorMessage(new ApiError(413, 'Request failed'))).toMatch(/25 MB/); expect(uploadErrorMessage(new ApiError(0, 'Network error'))).toMatch(/API running/); expect(uploadErrorMessage(new ApiError(400, 'The PDF could not be opened.'))).toMatch(/password/) }) })
