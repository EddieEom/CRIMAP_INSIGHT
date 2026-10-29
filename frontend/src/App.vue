<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import {
  AlertTriangle, BarChart3, Database, LoaderCircle, MapPinned,
  RefreshCw, Search, ShieldAlert, ShieldCheck, Siren, Target,
  ArrowRightLeft, ChevronRight, Clock3
} from 'lucide-vue-next'
import ProvinceTileMap from './components/ProvinceTileMap.vue'
import LineMini from './components/LineMini.vue'
import { api, number as fmt, shortDate, type Breakdown, type Comparison, type Meta, type Region, type Run, type Summary, type Trend } from './lib/api'

type Tab = 'overview' | 'compare' | 'pipeline' | 'guide'
const tabs:{key:Tab;label:string}[] = [
  {key:'overview',label:'현황판'},
  {key:'compare',label:'지역 비교'},
  {key:'pipeline',label:'데이터 확인'},
  {key:'guide',label:'이 사이트는?'}
]
const current = ref<Tab>('overview')
const meta = ref<Meta|null>(null)
const runs = ref<Run[]>([])
const year = ref(2025)
const major = ref('')
const region = ref('')
const regionA = ref('')
const regionB = ref('')
const province = ref('전체')
const search = ref('')
const rowsPerPage = ref(10)
const summary = ref<Summary|null>(null)
const breakdown = ref<Breakdown|null>(null)
const trends = ref<Trend|null>(null)
const comparison = ref<Comparison|null>(null)
const availableRegions = ref<Region[]>([])
const categoryNames = ref<string[]>([])
const busy = ref(false)
const detailBusy = ref(false)
const detailError = ref('')
const comparisonBusy = ref(false)
const syncBusy = ref(false)
const error = ref('')
const key = ref('')
const syncResult = ref('')
const syncFailed = ref(false)

let yearToken = 0
let summaryToken = 0
let detailToken = 0
let compareToken = 0

const synced = computed(() => Boolean(meta.value?.syncedYears.includes(year.value)))
const newestRun = computed(() => runs.value[0] ?? null)
const provinces = computed(() => ['전체', ...new Set((summary.value?.regions ?? []).map(x => x.province))])
const filteredRegions = computed(() => {
  const source = [...(summary.value?.regions ?? [])].sort((a,b)=>b.count-a.count)
  return source.filter(x =>
    (province.value === '전체' || x.province === province.value) &&
    (!search.value.trim() || `${x.region} ${x.province}`.toLowerCase().includes(search.value.trim().toLowerCase()))
  )
})
const pagedRegions = computed(() => filteredRegions.value.slice(0, rowsPerPage.value))
const maxRegionCount = computed(() => Math.max(1, ...filteredRegions.value.map(x=>x.count), 1))
const topCategory = computed(() => summary.value?.categories[0] ?? null)
const topRegion = computed(() => filteredRegions.value[0] ?? summary.value?.regions[0] ?? null)
const totalRegions = computed(() => summary.value?.regionCount ?? 0)
const topEight = computed(() => (summary.value?.categories ?? []).slice(0, 8))
const maxCategoryCount = computed(() => Math.max(1, ...topEight.value.map(x=>x.count), 1))
const compareCategories = computed(() => [...new Set((comparison.value?.breakdown ?? []).map(x => x.category))])
const compareTotal = (name:string) => comparison.value?.breakdown.filter(x=>x.region===name).reduce((sum,cur)=>sum+cur.count,0) ?? 0
const compareValue = (name:string, category:string) => comparison.value?.breakdown.find(x=>x.region===name && x.category===category)?.count ?? 0
const compareLead = computed(() => {
  if (!comparison.value) return ''
  const a = compareTotal(comparison.value.regionA)
  const b = compareTotal(comparison.value.regionB)
  if (a === b) return '두 지역의 총 건수가 같습니다.'
  return a > b
    ? `${comparison.value.regionA}가 ${comparison.value.regionB}보다 ${fmt(a-b)}건 많습니다.`
    : `${comparison.value.regionB}가 ${comparison.value.regionA}보다 ${fmt(b-a)}건 많습니다.`
})

function simpleStatus(s: Run['status']) {
  return ({Succeeded:'수집 완료', Failed:'실패', Running:'수집 중', Unchanged:'변경 없음'})[s]
}
function pct(n:number, total:number){ return total ? `${((n/total)*100).toFixed(1)}%` : '0%' }
function selectProvince(name:string){
  const alias: Record<string,string[]> = {
    경기:['경기도','경기'], 강원:['강원도','강원특별자치도','강원'], 충북:['충북','충청북도'], 충남:['충남','충청남도'],
    전북:['전북','전라북도','전북특별자치도'], 전남:['전남','전라남도'], 경북:['경북','경상북도'], 경남:['경남','경상남도'],
    제주:['제주','제주도','제주특별자치도'], 세종:['세종','세종시','세종특별자치시']
  }
  const found = provinces.value.find(p => p===name || p.startsWith(name) || (alias[name] ?? []).includes(p))
  if (found) province.value = found
}
async function refresh() {
  busy.value = true
  error.value = ''
  try {
    const [m, r] = await Promise.all([api.meta(), api.runs()])
    meta.value = m
    runs.value = r
    if (!m.availableYears.includes(year.value)) year.value = m.availableYears[0] ?? 2025
    await loadYearData()
  } catch (e) {
    error.value = (e as Error).message
    busy.value = false
  }
}
async function loadYearData(){
  const token = ++yearToken
  busy.value = true
  summary.value = null
  comparison.value = null
  breakdown.value = null
  trends.value = null
  if (!synced.value) {
    availableRegions.value = []
    categoryNames.value = []
    busy.value = false
    return
  }
  try {
    const [regions, categories] = await Promise.all([api.regions(year.value), api.categories(year.value)])
    if (token !== yearToken) return
    availableRegions.value = regions
    categoryNames.value = [...new Set(categories.map(x => x.categoryMajor))]
    if (major.value && !categoryNames.value.includes(major.value)) major.value = ''
    if (!regions.some(x => x.region === region.value)) region.value = regions[0]?.region ?? ''
    if (!regions.some(x => x.region === regionA.value)) regionA.value = regions[0]?.region ?? ''
    if (!regions.some(x => x.region === regionB.value) || regionA.value === regionB.value) regionB.value = regions.find(x => x.region !== regionA.value)?.region ?? ''
    await Promise.all([loadSummary(), loadDetail(), loadComparison()])
  } catch (e) {
    if (token === yearToken) error.value = (e as Error).message
  } finally {
    if (token === yearToken) busy.value = false
  }
}
async function loadSummary(){
  const token = ++summaryToken
  if (!synced.value) return
  try {
    const data = await api.summary(year.value, major.value || undefined)
    if (token === summaryToken) summary.value = data
  } catch (e) {
    if (token === summaryToken) error.value = (e as Error).message
  }
}
async function loadDetail(){
  const token = ++detailToken
  breakdown.value = null
  trends.value = null
  detailError.value = ''
  if (!synced.value || !region.value) return
  detailBusy.value = true
  try {
    const [b, t] = await Promise.all([
      api.breakdown(year.value, region.value, major.value || undefined),
      api.trends(region.value, major.value || undefined)
    ])
    if (token === detailToken) {
      breakdown.value = b
      trends.value = t
    }
  } catch (e) {
    if (token === detailToken) detailError.value = (e as Error).message
  } finally {
    if (token === detailToken) detailBusy.value = false
  }
}
async function loadComparison(){
  const token = ++compareToken
  if (!synced.value || !regionA.value || !regionB.value || regionA.value === regionB.value) {
    comparison.value = null
    return
  }
  comparisonBusy.value = true
  try {
    const data = await api.compare(year.value, regionA.value, regionB.value)
    if (token === compareToken) comparison.value = data
  } catch (e) {
    if (token === compareToken) error.value = (e as Error).message
  } finally {
    if (token === compareToken) comparisonBusy.value = false
  }
}
async function sync(){
  if (!key.value.trim()) {
    syncFailed.value = true
    syncResult.value = '관리자 키를 먼저 입력하세요.'
    return
  }
  syncBusy.value = true
  syncFailed.value = false
  syncResult.value = ''
  try {
    const res = await api.sync(year.value, key.value)
    syncResult.value = `${res.message} / 원본 ${fmt(res.run.rawRows)}행 · 저장 ${fmt(res.run.observations)}개`
    await refresh()
  } catch (e) {
    syncFailed.value = true
    syncResult.value = (e as Error).message
  } finally {
    syncBusy.value = false
  }
}
function jumpToDetail(value:string){
  region.value = value
  const el = document.getElementById('detail-panel')
  if (el) el.scrollIntoView({ behavior:'smooth', block:'start' })
}

watch(year, async () => { major.value=''; province.value='전체'; rowsPerPage.value=10; if (meta.value) await loadYearData() })
watch(major, async () => { rowsPerPage.value=10; await Promise.all([loadSummary(), loadDetail()]) })
watch(region, async () => { await loadDetail() })
watch([regionA, regionB], async () => { await loadComparison() })
watch([province, search], () => { rowsPerPage.value = 10 })
onMounted(async () => { await refresh() })
</script>

<template>
  <div class="danger-app">
    <header class="topbar">
      <div>
        <p class="eyebrow">CRIMAP INSIGHT / 범죄 현황판</p>
        <h1>한눈에 보는 지역별 범죄 통계</h1>
        <p class="subcopy">어느 지역에서 많이 발생했는지, 어떤 범죄가 많은지 바로 확인할 수 있게 만든 경각심용 대시보드입니다.</p>
      </div>
      <div class="top-actions">
        <button class="ghost-btn" @click="refresh" :disabled="busy"><RefreshCw :size="16" :class="{spin:busy}"/> 새로고침</button>
        <div class="top-status" :class="{off:!synced}">
          <span class="dot"></span>
          <b>{{ synced ? '데이터 준비됨' : '동기화 필요' }}</b>
        </div>
      </div>
    </header>

    <section class="warning-strip">
      <div><Siren :size="18"/> <b>주의</b> 이 수치는 실시간 신고 현황이 아니라 연도별 공표 통계입니다.</div>
      <div v-if="newestRun"><Clock3 :size="16"/> 마지막 수집: {{ shortDate(newestRun.startedAtUtc) }}</div>
    </section>

    <section class="control-panel panel">
      <div class="controls-grid">
        <label>
          <span>연도</span>
          <select v-model.number="year">
            <option v-for="y in meta?.availableYears ?? []" :key="y" :value="y">{{ y }}년</option>
          </select>
        </label>
        <label>
          <span>범죄 분류</span>
          <select v-model="major">
            <option value="">전체</option>
            <option v-for="c in categoryNames" :key="c" :value="c">{{ c }}</option>
          </select>
        </label>
        <label>
          <span>시/도</span>
          <select v-model="province">
            <option v-for="p in provinces" :key="p" :value="p">{{ p }}</option>
          </select>
        </label>
        <label>
          <span>지역 검색</span>
          <div class="search-box">
            <Search :size="16"/>
            <input v-model="search" type="text" placeholder="예: 부산 남구" />
          </div>
        </label>
      </div>
      <p class="help-line">필터를 바꾸면 아래 지도, 순위, 상세 통계가 함께 바뀝니다.</p>
    </section>

    <div v-if="error" class="error-box"><AlertTriangle :size="17"/> {{ error }}</div>

    <nav class="tabbar">
      <button v-for="tab in tabs" :key="tab.key" :class="['tab-btn',{active:current===tab.key}]" @click="current=tab.key">{{ tab.label }}</button>
    </nav>

    <main v-if="current==='overview'" class="page-stack">
      <section class="hero-grid">
        <article class="panel kpi-card danger">
          <div class="kpi-top"><ShieldAlert :size="20"/><span>총 발생 건수</span></div>
          <div class="kpi-main">{{ summary ? fmt(summary.total) : '-' }}<small>건</small></div>
          <p>{{ year }}년 {{ major || '전체 범죄' }} 기준입니다.</p>
        </article>
        <article class="panel kpi-card">
          <div class="kpi-top"><MapPinned :size="20"/><span>가장 많은 지역</span></div>
          <div class="kpi-main small-main">{{ topRegion?.region ?? '-' }}</div>
          <p v-if="topRegion">{{ fmt(topRegion.count) }}건 / {{ topRegion.province }}</p>
          <p v-else>데이터 없음</p>
        </article>
        <article class="panel kpi-card">
          <div class="kpi-top"><BarChart3 :size="20"/><span>가장 많은 범죄</span></div>
          <div class="kpi-main small-main">{{ topCategory?.category ?? '-' }}</div>
          <p v-if="topCategory">{{ fmt(topCategory.count) }}건 · {{ pct(topCategory.count, summary?.total ?? 0) }}</p>
          <p v-else>데이터 없음</p>
        </article>
        <article class="panel kpi-card muted-card">
          <div class="kpi-top"><Database :size="20"/><span>집계 지역 수</span></div>
          <div class="kpi-main">{{ fmt(totalRegions) }}<small>개</small></div>
          <p>시군구 기준으로 정리된 데이터입니다.</p>
        </article>
      </section>

      <section class="board-grid">
        <article class="panel map-panel">
          <div class="panel-head compact-head">
            <div>
              <p class="mini-eyebrow">CRIME MAP</p>
              <h2>시도별 범죄 맵</h2>
            </div>
            <button class="mini-btn" @click="province='전체'">전체 보기</button>
          </div>
          <ProvinceTileMap :rows="summary?.regions ?? []" :selected="province === '전체' ? '' : province" :active="synced" @select="selectProvince" />
        </article>

        <article class="panel category-panel">
          <div class="panel-head compact-head">
            <div>
              <p class="mini-eyebrow">TOP CRIME</p>
              <h2>범죄 유형 비중</h2>
            </div>
          </div>
          <div class="bar-list">
            <div v-for="cat in topEight" :key="cat.category" class="bar-row">
              <div class="bar-text">
                <b>{{ cat.category }}</b>
                <span>{{ fmt(cat.count) }}건</span>
              </div>
              <div class="bar-track"><div class="bar-fill" :style="{width: `${(cat.count / maxCategoryCount) * 100}%`}"></div></div>
            </div>
          </div>
        </article>
      </section>

      <section class="board-grid lower">
        <article class="panel rank-panel">
          <div class="panel-head compact-head">
            <div>
              <p class="mini-eyebrow">RANKING</p>
              <h2>지역별 발생 순위</h2>
            </div>
            <button class="mini-btn" @click="rowsPerPage += 10" :disabled="rowsPerPage >= filteredRegions.length">더 보기</button>
          </div>
          <div class="rank-list">
            <button v-for="(row, idx) in pagedRegions" :key="row.region" class="rank-item" @click="jumpToDetail(row.region)">
              <div class="rank-order">{{ idx + 1 }}</div>
              <div class="rank-info">
                <b>{{ row.region }}</b>
                <span>{{ row.province }}</span>
              </div>
              <div class="rank-bar"><div class="rank-fill" :style="{ width: `${(row.count / maxRegionCount) * 100}%` }"></div></div>
              <div class="rank-count">{{ fmt(row.count) }}건</div>
              <ChevronRight :size="16"/>
            </button>
          </div>
        </article>

        <article class="panel detail-panel" id="detail-panel">
          <div class="panel-head compact-head">
            <div>
              <p class="mini-eyebrow">DETAIL</p>
              <h2>선택 지역 상세</h2>
            </div>
            <select v-model="region" class="inline-select">
              <option v-for="r in availableRegions" :key="r.region" :value="r.region">{{ r.region }}</option>
            </select>
          </div>

          <div v-if="detailBusy" class="loading-box"><LoaderCircle :size="18" class="spin"/> {{ region }} 정보를 불러오는 중...</div>
          <div v-else-if="detailError" class="detail-message error-box"><AlertTriangle :size="18"/><span>상세 조회 실패: {{ detailError }}</span><button class="mini-btn" @click="loadDetail">다시 조회</button></div>
          <template v-else-if="breakdown">
            <div class="detail-top">
              <div>
                <p class="detail-label">선택 지역</p>
                <h3>{{ breakdown.region }}</h3>
              </div>
              <div class="detail-total">총 {{ fmt(breakdown.total) }}건</div>
            </div>
            <div v-if="breakdown.categories.length === 0" class="blank-box">이 조건에 해당하는 범죄 기록이 없습니다.</div>
            <div v-else class="minor-list">
              <div v-for="item in breakdown.categories.slice(0, 10)" :key="`${item.major}-${item.minor}`" class="minor-row">
                <div>
                  <b>{{ item.minor }}</b>
                  <span>{{ item.major }}</span>
                </div>
                <strong>{{ fmt(item.count) }}건</strong>
              </div>
            </div>
            <div class="trend-section">
              <p class="trend-title">연도별 변화</p>
              <LineMini :points="trends?.points ?? []" />
            </div>
          </template>
          <div v-else class="blank-box">지역을 선택하면 자세한 내용을 볼 수 있습니다. <button class="mini-btn" @click="loadDetail">조회하기</button></div>
        </article>
      </section>
    </main>

    <main v-else-if="current==='compare'" class="page-stack">
      <section class="panel compare-panel">
        <div class="panel-head">
          <div>
            <p class="mini-eyebrow">COMPARE</p>
            <h2>지역 두 곳 비교하기</h2>
            <p class="panel-desc">어느 지역이 더 많은지 바로 볼 수 있게 단순하게 정리했습니다.</p>
          </div>
        </div>
        <div class="compare-controls">
          <label><span>지역 A</span><select v-model="regionA"><option v-for="r in availableRegions" :key="r.region" :value="r.region">{{ r.region }}</option></select></label>
          <label><span>지역 B</span><select v-model="regionB"><option v-for="r in availableRegions" :key="r.region" :value="r.region">{{ r.region }}</option></select></label>
        </div>
        <div v-if="comparisonBusy" class="loading-box"><LoaderCircle :size="18" class="spin"/> 비교 중...</div>
        <template v-else-if="comparison">
          <div class="compare-alert"><ArrowRightLeft :size="18"/><b>{{ compareLead }}</b></div>
          <div class="compare-kpis">
            <div class="compare-box red-box"><span>{{ comparison.regionA }}</span><b>{{ fmt(compareTotal(comparison.regionA)) }}건</b></div>
            <div class="compare-box dark-box"><span>{{ comparison.regionB }}</span><b>{{ fmt(compareTotal(comparison.regionB)) }}건</b></div>
          </div>
          <div class="compare-table">
            <div class="compare-row head"><span>범죄 종류</span><span>{{ comparison.regionA }}</span><span>{{ comparison.regionB }}</span></div>
            <div v-for="cat in compareCategories" :key="cat" class="compare-row">
              <span>{{ cat }}</span>
              <span>{{ fmt(compareValue(comparison.regionA, cat)) }}</span>
              <span>{{ fmt(compareValue(comparison.regionB, cat)) }}</span>
            </div>
          </div>
        </template>
      </section>
    </main>

    <main v-else-if="current==='pipeline'" class="page-stack">
      <section class="panel pipeline-panel">
        <div class="panel-head">
          <div>
            <p class="mini-eyebrow">DATA CHECK</p>
            <h2>데이터 수집 상태</h2>
            <p class="panel-desc">경찰청 공공데이터를 가져와 저장한 기록입니다.</p>
          </div>
        </div>
        <div class="sync-box">
          <label><span>관리자 키</span><input v-model="key" type="password" placeholder="동기화용 키 입력" /></label>
          <button class="danger-btn" @click="sync" :disabled="syncBusy"><LoaderCircle v-if="syncBusy" :size="16" class="spin"/><RefreshCw v-else :size="16"/> {{ year }}년 다시 수집</button>
        </div>
        <div v-if="syncResult" class="sync-result" :class="{failed:syncFailed}">{{ syncResult }}</div>
        <div class="pipeline-grid">
          <div class="mini-card"><span>마지막 상태</span><b>{{ newestRun ? simpleStatus(newestRun.status) : '-' }}</b></div>
          <div class="mini-card"><span>원본 행 수</span><b>{{ newestRun ? fmt(newestRun.rawRows) : '-' }}</b></div>
          <div class="mini-card"><span>저장된 관측값</span><b>{{ newestRun ? fmt(newestRun.observations) : '-' }}</b></div>
          <div class="mini-card"><span>지역 수</span><b>{{ newestRun ? fmt(newestRun.regions) : '-' }}</b></div>
        </div>
        <div class="run-list">
          <div v-for="run in runs" :key="run.id" class="run-item">
            <div>
              <b>{{ run.year }}년 / {{ simpleStatus(run.status) }}</b>
              <p>{{ shortDate(run.startedAtUtc) }}</p>
            </div>
            <div class="run-side">
              <span>{{ fmt(run.observations) }}개 저장</span>
              <small>{{ run.sha256?.slice(0, 12) ?? '-' }}</small>
            </div>
          </div>
        </div>
      </section>
    </main>

    <main v-else class="page-stack">
      <section class="panel guide-panel">
        <div class="panel-head">
          <div>
            <p class="mini-eyebrow">GUIDE</p>
            <h2>이 사이트는 무엇을 보여주나요?</h2>
          </div>
        </div>
        <div class="guide-list">
          <div>
            <ShieldCheck :size="18"/>
            <div><b>목적</b><p>범죄가 어느 지역에서 얼마나 많이 발생했는지 쉽게 보이게 해서 경각심을 주기 위한 사이트입니다.</p></div>
          </div>
          <div>
            <Target :size="18"/>
            <div><b>주의할 점</b><p>이 수치는 연간 통계라서, 오늘 당장 위험한 장소를 알려주는 실시간 경보는 아닙니다.</p></div>
          </div>
          <div>
            <Database :size="18"/>
            <div><b>출처</b><p>{{ meta?.source ?? '경찰청 공공데이터' }} / 지역 단위로 정리된 통계를 사용합니다.</p></div>
          </div>
          <div>
            <AlertTriangle :size="18"/>
            <div><b>해석</b><p>건수가 많다고 해서 무조건 개인 위험도가 높다고 단정할 수는 없습니다. 인구 규모와 지역 특성도 함께 봐야 합니다.</p></div>
          </div>
        </div>
      </section>
    </main>
  </div>
</template>
