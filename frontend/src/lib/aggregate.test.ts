import { describe, expect, it } from 'vitest'
import { classify, expenseByCategory, expenseStacks, incomeExpenseByPeriod, signedTotal } from './aggregate'
import type { SpendingRow } from '../api/types'
const row = (overrides: Partial<SpendingRow> = {}): SpendingRow => ({ periodStart: '2026-08-01', categoryId: 'food', categoryName: 'Food', categoryKind: 'Expense', direction: 'Debit', total: 10, count: 1, ...overrides })
describe('aggregate', () => {
  it('lets category kind override direction', () => { expect(classify(row({ categoryKind: 'Income', direction: 'Debit' }))).toBe('income'); expect(signedTotal(row({ categoryKind: 'Expense', direction: 'Credit' }))).toBe(-10) })
  it('falls back to direction for uncategorized rows', () => { expect(classify(row({ categoryId: null, categoryName: null, categoryKind: null, direction: 'Credit' }))).toBe('income') })
  it('nets refunds against expense without moving them to income', () => { const values = incomeExpenseByPeriod([row({ total: 100 }), row({ direction: 'Credit', total: 25 })]); expect(values[0]).toEqual({ periodStart: '2026-08-01', income: 0, expense: 75 }) })
  it('omits zero-net categories and preserves refund-only categories', () => { const result = expenseByCategory([row({ total: 20 }), row({ direction: 'Credit', total: 20 }), row({ categoryId: 'refund', categoryName: 'Refunds', direction: 'Credit', total: 8 })]); expect(result.slices).toHaveLength(0); expect(result.refundOnly[0].name).toBe('Refunds') })
  it('keeps uncategorized separate and preserves negative stack values', () => { const result = expenseStacks([row({ categoryId: null, categoryName: null, direction: 'Credit', total: 5 })], 6); expect(result.series[0].key).toBe('uncategorized'); expect(result.periods[0].uncategorized).toBe(-5) })
  it('collapses categories beyond topN into Other', () => { const rows = Array.from({ length: 3 }, (_, index) => row({ categoryId: `cat-${index}`, categoryName: `Cat ${index}`, total: 10 - index })); const result = expenseStacks(rows, 2); expect(result.series.map(item => item.key)).toContain('other') })
})
