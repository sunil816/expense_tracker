import { ApiError } from '../api/client'
export function uploadErrorMessage(error: unknown): string {
  if (!(error instanceof ApiError)) return 'Upload failed. Please try again.'
  if (error.status === 413) return 'The file is too large for the server (25 MB limit).'
  if (error.status === 429) return 'The server is busy with another extraction. Try again in a few minutes.'
  if (error.status === 503) return 'The PDF decryption tool is unavailable on the server.'
  if (error.status === 504) return 'Extraction timed out — the document may be too large.'
  if (error.status === 502) return 'The extraction service failed. Check the server logs.'
  if (error.status === 0) return 'Network problem — is the API running?'
  if (error.status === 422 && error.title === 'No supported transaction data was found.') return 'No transaction table was found in this document.'
  if (error.status === 400 && error.title === 'The PDF could not be opened.') return 'Wrong password, or the PDF is corrupted. Check the password and retry.'
  if (error.status === 400 && ['The uploaded file is invalid.', 'A PDF file is required.'].includes(error.title)) return "That file doesn't look like a supported PDF."
  return `Upload failed (${error.title}).`
}
