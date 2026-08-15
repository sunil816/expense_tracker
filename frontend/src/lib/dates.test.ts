import { describe, expect, it } from 'vitest'
import { addMonthsIso, monthStartIso } from './dates'
describe('dates', () => { it('does calendar month arithmetic across year boundaries', () => { expect(addMonthsIso('2026-01-31', -2)).toBe('2025-11-01'); expect(addMonthsIso('2026-12-20', 1)).toBe('2027-01-01') }); it('takes month starts without UTC shifting', () => { expect(monthStartIso('2026-08-12')).toBe('2026-08-01') }) })
