<script setup lang="ts">
import { computed } from 'vue'
const props = defineProps<{ points:{ year:number; count:number }[] }>()
const max = computed(() => Math.max(1,...props.points.map(p => p.count)))
const x = (i:number) => 30+i*(490/Math.max(1,props.points.length-1))
const y = (count:number) => 148-(count/max.value)*116
const coords = computed(() => props.points.map((p,i)=>`${x(i)},${y(p.count)}`).join(' '))
</script>
<template>
  <div v-if="points.length>=2" class="trend-wrap">
    <svg viewBox="0 0 550 180" aria-label="연도별 범죄 발생 건수 추이 (0을 기준으로 표현)" preserveAspectRatio="none" role="img">
      <line x1="20" y1="148" x2="530" y2="148" class="chart-grid"/>
      <line x1="20" y1="90" x2="530" y2="90" class="chart-grid"/>
      <line x1="20" y1="32" x2="530" y2="32" class="chart-grid"/>
      <polyline :points="coords" fill="none" stroke="var(--accent)" stroke-width="3" stroke-linecap="round" stroke-linejoin="round"/>
      <circle v-for="(p,i) in points" :key="p.year" :cx="x(i)" :cy="y(p.count)" r="4.5" fill="var(--accent)" stroke="white" stroke-width="2"/>
    </svg>
    <div class="trend-labels"><span v-for="p in points" :key="p.year"><b>{{ p.year }}</b><small>{{ p.count.toLocaleString('ko-KR') }}건</small></span></div>
  </div>
  <div v-else-if="points.length===1" class="blank-state">현재 {{points[0].year}}년 데이터만 존재합니다. 최소 두 개 연도를 동기화해야 추세를 비교할 수 있습니다.</div>
  <div v-else class="blank-state">추세를 표시할 동기화된 연도 데이터가 없습니다.</div>
</template>
