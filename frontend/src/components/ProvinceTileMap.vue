<script setup lang="ts">
import { computed } from 'vue'
import { MapPinned } from 'lucide-vue-next'
import type { RankedRegion } from '../lib/api'

const props = defineProps<{ rows: RankedRegion[]; selected: string; active: boolean }>()
const emit = defineEmits<{ (e: 'select', name: string): void }>()
const provinces = [
  { name: '서울', x: 2, y: 2 }, { name: '인천', x: 1, y: 2 }, { name: '경기', x: 3, y: 2 }, { name: '강원', x: 5, y: 2 },
  { name: '충남', x: 2, y: 3 }, { name: '세종', x: 3, y: 3 }, { name: '충북', x: 4, y: 3 },
  { name: '대전', x: 3, y: 4 }, { name: '경북', x: 5, y: 4 },
  { name: '전북', x: 2, y: 5 }, { name: '대구', x: 4, y: 5 }, { name: '울산', x: 6, y: 5 },
  { name: '광주', x: 2, y: 6 }, { name: '전남', x: 3, y: 6 }, { name: '경남', x: 4, y: 6 }, { name: '부산', x: 5, y: 6 },
  { name: '제주', x: 2, y: 8 }
]
function simplify(n: string) {
  const x = n.replace(/(특별자치시|특별자치도|특별시|광역시|자치도|도|시)$/,'')
  if (/^충청북/.test(x)) return '충북'
  if (/^충청남/.test(x)) return '충남'
  if (/^전라북/.test(x)) return '전북'
  if (/^전라남/.test(x)) return '전남'
  if (/^경상북/.test(x)) return '경북'
  if (/^경상남/.test(x)) return '경남'
  return x
}
const grouped = computed(() => {
  const map = new Map<string, number>()
  for (const row of props.rows) {
    const key = simplify(row.province)
    map.set(key, (map.get(key) ?? 0) + row.count)
  }
  return map
})
const maximum = computed(() => Math.max(1, ...Array.from(grouped.value.values())))
function density(name: string) {
  const n = grouped.value.get(name)
  if (!n) return 'none'
  const p = n / maximum.value
  if (p > .7) return 'high'
  if (p > .36) return 'medium'
  if (p > .12) return 'low'
  return 'verylow'
}
const total = (name:string) => grouped.value.get(name) ?? 0
</script>

<template>
  <div class="tilemap-wrap">
    <div class="tile-map" aria-label="시도별 범죄 발생 건수를 보여주는 범죄 맵">
      <button
        v-for="p in provinces"
        :key="p.name"
        type="button"
        class="province-tile"
        :class="[`tone-${density(p.name)}`, { chosen: selected === p.name }]"
        :style="{ '--gx': p.x, '--gy': p.y }"
        :title="`${p.name}: ${total(p.name).toLocaleString('ko-KR')}건`"
        :disabled="!active || !total(p.name)"
        @click="emit('select', p.name)"
      >
        <span>{{ p.name }}</span>
      </button>
      <div v-if="!active" class="map-empty">동기화된 데이터가 없습니다.</div>
    </div>

    <div class="map-side">
      <span class="mini-eyebrow">CRIME MAP</span>
      <MapPinned :size="26" />
      <p>붉을수록 그 시·도의 총 발생 건수가 많습니다.</p>
      <div class="legend-step"><i class="tone-verylow"></i><i class="tone-low"></i><i class="tone-medium"></i><i class="tone-high"></i></div>
      <div class="legend-label"><span>적음</span><span>많음</span></div>
      <small>실제 지도가 아니라 보기 쉽게 단순화한 범죄 맵입니다.</small>
    </div>
  </div>
</template>
