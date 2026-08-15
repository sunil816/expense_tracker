const parts = (iso: string) => { const [year, month, day] = iso.split('-').map(Number); return { year, month, day } }
const pad = (n: number) => String(n).padStart(2, '0')
export const todayIso = () => { const d = new Date(); return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}` }
export const monthStartIso = (iso: string) => { const { year, month } = parts(iso); return `${year}-${pad(month)}-01` }
export const addMonthsIso = (iso: string, months: number) => { const { year, month } = parts(iso); const d = new Date(year, month - 1 + months, 1); return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-01` }
export const presetRange = (preset: 'thisMonth' | 'last3' | 'last6') => { const today = todayIso(); const start = monthStartIso(today); return { from: addMonthsIso(start, preset === 'thisMonth' ? 0 : preset === 'last3' ? -2 : -5), to: today } }
