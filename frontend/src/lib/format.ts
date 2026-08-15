export const formatInr = (amount: number) => `₹${Math.abs(amount).toLocaleString('en-IN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`
export const signedAmount = (amount: number) => `${amount < 0 ? '−' : '+'} ${formatInr(amount)}`
export const formatDate = (iso: string) => { const [year, month, day] = iso.split('-').map(Number); return new Intl.DateTimeFormat('en-IN', { day: 'numeric', month: 'short', year: 'numeric' }).format(new Date(year, month - 1, day)) }
export const formatDateTime = (iso: string) => new Intl.DateTimeFormat('en-IN', { day: 'numeric', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit', hour12: false }).format(new Date(iso)).replace(' at ', ', ')
export const formatPeriodLabel = (granularity: 'week' | 'month', periodStart: string) => granularity === 'month' ? formatDate(periodStart).replace(/^\d+ /, '') : `Wk of ${formatDate(periodStart).replace(/ \d{4}$/, '')}`
