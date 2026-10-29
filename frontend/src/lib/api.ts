export type Year = number
export type Meta = { availableYears: number[]; syncedYears: number[]; source: string; granularity: string; caveat: string }
export type Region = { region: string; province: string }
export type Category = { categoryMajor: string; categoryMinor: string }
export type RankedRegion = Region & { count: number }
export type Segment = { category: string; count: number }
export type Summary = {
  year: number; category: string | null; total: number; regionCount: number;
  categories: Segment[]; regions: RankedRegion[]; note: string
}
export type Breakdown = { year: number; region: string; total: number; categories: { major: string; minor: string; count: number }[] }
export type Comparison = {
  year: number; regionA: string; regionB: string;
  breakdown: { region: string; category: string; count: number }[]; caveat: string
}
export type Trend = { region: string; category: string | null; points: { year: number; count: number }[]; caveat: string }
export type Run = {
  id: string; year: number; status: 'Running' | 'Failed' | 'Succeeded' | 'Unchanged';
  startedAtUtc: string; completedAtUtc: string | null; rawRows: number; observations: number;
  regions: number; sha256: string | null; errorSummary: string | null; sourceEndpoint: string
}

const BASE = import.meta.env.VITE_API_BASE_URL?.replace(/\/$/, '') ?? ''
export class ApiError extends Error {
  constructor(message: string, public readonly status: number) { super(message) }
}
async function request<T>(path: string, options: RequestInit = {}): Promise<T> {
  let response: Response
  try { response = await fetch(`${BASE}/api${path}`, { ...options, headers: { Accept: 'application/json', ...(options.headers ?? {}) } }) }
  catch { throw new ApiError('서버에 연결할 수 없습니다. ASP.NET Core API 실행 상태를 확인하세요.', 0) }
  const json = await response.json().catch(() => null)
  if (!response.ok) {
    const info = json?.error || json?.message || json?.detail || `요청 실패 (HTTP ${response.status})`
    throw new ApiError(String(info), response.status)
  }
  return json as T
}
const params = (record: Record<string, string | number | undefined>) => {
  const qs = new URLSearchParams()
  Object.entries(record).forEach(([key, value]) => { if (value !== undefined && value !== '') qs.set(key, String(value)) })
  return qs.toString()
}
export const api = {
  meta: () => request<Meta>('/meta'),
  regions: (year: number) => request<Region[]>(`/regions?${params({ year })}`),
  categories: (year: number) => request<Category[]>(`/categories?${params({ year })}`),
  summary: (year: number, major?: string) => request<Summary>(`/summary?${params({ year, major })}`),
  breakdown: (year: number, region: string, major?: string) => request<Breakdown>(`/breakdown?${params({ year, region, major })}`),
  compare: (year: number, regionA: string, regionB: string) => request<Comparison>(`/compare?${params({ year, regionA, regionB })}`),
  trends: (region: string, major?: string) => request<Trend>(`/trends?${params({ region, major })}`),
  runs: () => request<Run[]>('/pipeline/runs'),
  sync: (year: number, key: string) => request<{ run: Run; message: string }>(`/admin/sync/${year}`, {
    method: 'POST', headers: { 'X-Admin-Key': key }
  }),
}
export const number = (n: number) => new Intl.NumberFormat('ko-KR').format(n)
export const shortDate = (s: string) => new Date(s).toLocaleString('ko-KR', { dateStyle: 'medium', timeStyle: 'short' })
