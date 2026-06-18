<script setup lang="ts">
import { reactive, ref, watch, computed } from 'vue';
import { Message } from "@arco-design/web-vue";
import { RyzenSmu } from "@/utils/bridge";
import { useConfigStore } from '@/stores/config';

const CONFIG_GROUPS = [
  {
    title: 'Power Limits',
    items: [
      {label: 'STAPM Limit', key: 'StapmLimit', min: 0, max: 200, unit: 'W'},
      {label: 'STAPM Time', key: 'StapmTime', min: 0, max: 3600, unit: 's'},
      {label: 'Fast Limit', key: 'FastLimit', min: 0, max: 200, unit: 'W'},
      {label: 'Slow Limit', key: 'SlowLimit', min: 0, max: 200, unit: 'W'},
      {label: 'Slow Time', key: 'SlowTime', min: 0, max: 3600, unit: 's'},
      {label: 'PPT Limit (RSMU)', key: 'PptLimitRsmu', min: 0, max: 200, unit: 'W'},
    ]
  },
  {
    title: 'Current Limits',
    items: [
      {label: 'VRM Current (MP1)', key: 'VrmCurrentMp1', min: 0, max: 300000, step: 1000, unit: 'mA'},
      {label: 'VRM Current (RSMU)', key: 'VrmCurrentRsmu', min: 0, max: 300000, step: 1000, unit: 'mA'},
      {label: 'EDC Limit (MP1)', key: 'EdcLimitMp1', min: 0, max: 300000, step: 1000, unit: 'mA'},
      {label: 'EDC Limit (RSMU)', key: 'EdcLimitRsmu', min: 0, max: 300000, step: 1000, unit: 'mA'},
    ]
  },
  {
    title: 'Thermal Control',
    items: [
      {label: 'Temp Limit (MP1)', key: 'TempLimitMp1', min: 40, max: 115, unit: '℃'},
      {label: 'Temp Limit (RSMU)', key: 'TempLimitRsmu', min: 40, max: 115, unit: '℃'},
    ]
  },
  {
    title: 'Clocks & OC',
    items: [
      {label: 'PBO Scalar', key: 'PboScalar', min: 1, max: 100, unit: 'x'},
      {label: 'OC Clocks', key: 'OcClk', min: -500, max: 500, step: 25, unit: 'MHz'},
      {label: 'OC Volt', key: 'OcVolt', min: 0, max: 1550, step: 5, unit: 'mV'},
    ]
  }
];

const loadingMap = reactive<Record<string, boolean>>({});
const configStore = useConfigStore();
const smuData = computed(() => configStore.config?.RyzenSumConfig);

const coreCount = ref(8);
const perCoreCurve = reactive<number[]>([]);
const perCoreOcClk = reactive<number[]>([]);

watch(coreCount, (newCount) => {
  if (newCount < 0) return;
  const currentLen = perCoreCurve.length;
  if (newCount > currentLen) {
    for (let i = currentLen; i < newCount; i++) {
      perCoreCurve.push(0);
      perCoreOcClk.push(0);
    }
  } else if (newCount < currentLen) {
    perCoreCurve.splice(newCount);
    perCoreOcClk.splice(newCount);
  }
}, {immediate: true});

const applySetting = async (methodName: keyof typeof RyzenSmu, ...args: any[]) => {
  loadingMap[methodName] = true;
  try {
    const fn = RyzenSmu[methodName] as (...args: any[]) => Promise<any>;
    const res = await fn(...args);

    if (res && res.Success !== undefined) {
      res.Success ? Message.success(res.Message || 'Success') : Message.error(res.Message || 'Failed');
    } else {
      Message.success('Command executed successfully');
    }
    configStore.debouncedSave();
  } catch (e) {
    Message.error('Execution failed');
    console.error(e);
  } finally {
    loadingMap[methodName] = false;
  }
};
</script>

<template>
  <div class="p-6 h-full overflow-y-auto bg-gradient-to-br from-[#11121A] to-[#0D0E15] text-white" v-if="smuData">
    <div class="mb-8">
      <h1 class="text-3xl font-bold tracking-tight">Ryzen SMU</h1>
      <p class="text-gray-400 mt-1">高级电源、电流及频率限制调整 (AMD Ryzen 平台专用)</p>
    </div>

    <div class="columns-1 xl:columns-2 gap-8 space-y-8">
      <!-- 动态生成的配置卡片 -->
      <div v-for="group in CONFIG_GROUPS" :key="group.title" 
      class="break-inside-avoid bg-[#1A1B26]/60 border border-white/5 p-6 rounded-3xl shadow-xl hover:bg-[#1A1B26]/80 transition-all">
        <h3 class="text-xs font-black text-purple-500 uppercase tracking-widest mb-6 border-l-4 border-purple-600 pl-3">
          {{ group.title }}
        </h3>
        
        <div class="space-y-6">
          <div v-for="item in group.items" :key="item.key" class="space-y-2">
            <div class="flex justify-between items-center px-1">
              <span class="text-xs font-bold text-gray-400">{{ item.label }}</span>
              <span class="text-sm font-mono text-white">{{ smuData[item.key] }}{{ item.unit }}</span>
            </div>
            <div class="flex items-center gap-4">
              <a-slider
                v-model="smuData[item.key]"
                :min="item.min"
                :max="item.max"
                :step="item.step || 1"
                class="flex-1"
                :style="{ '--color-primary-6': '#8A2BE2' }"
              />
              <a-button
                type="primary"
                size="mini"
                class="!bg-purple-600 !border-none rounded-lg"
                :loading="loadingMap[item.key]"
                @click="applySetting(item.key as keyof typeof RyzenSmu, smuData[item.key])"
              >Apply</a-button>
            </div>
          </div>
        </div>

        <div v-if="group.title === 'Clocks & OC'" class="mt-8 flex gap-4 pt-6 border-t border-white/5">
          <a-button
            type="primary"
            status="success"
            class="flex-1 !rounded-xl font-bold"
            :loading="loadingMap['EnableOc']"
            @click="applySetting('EnableOc')"
          >Enable OC</a-button>
          <a-button
            type="primary"
            status="danger"
            class="flex-1 !rounded-xl font-bold"
            :loading="loadingMap['DisableOc']"
            @click="applySetting('DisableOc')"
          >Disable OC</a-button>
        </div>
      </div>

      <!-- Curve Optimizer 卡片 -->
      <div class="break-inside-avoid bg-[#1A1B26]/60 border border-white/5 p-6 rounded-3xl shadow-xl">
        <div class="flex justify-between items-center mb-6">
          <h3 class="text-xs font-black text-orange-500 uppercase tracking-widest border-l-4 border-orange-600 pl-3">
            Curve Optimizer
          </h3>
          <div class="flex items-center gap-2">
            <span class="text-[10px] font-bold text-gray-500 uppercase">Cores</span>
            <a-input-number
              v-model="coreCount"
              :min="1" :max="64"
              size="mini"
              class="!w-16 !bg-white/5 !border-white/10 !text-white rounded-lg"
              hide-button
            />
          </div>
        </div>

        <div class="bg-black/20 p-4 rounded-2xl mb-6 border border-white/5">
          <div class="flex justify-between items-center mb-2">
            <span class="text-xs font-bold text-gray-300">All Core Offset</span>
            <span class="text-xs font-mono text-orange-400">{{ smuData.CurveOptimizerAll }}</span>
          </div>
          <div class="flex items-center gap-4">
            <a-slider v-model="smuData.CurveOptimizerAll" :min="-100" :max="100" class="flex-1" :style="{ '--color-primary-6': '#ff7d00' }"/>
            <a-button
              type="primary"
              size="mini"
              class="!bg-orange-600 !border-none rounded-lg"
              :loading="loadingMap['SetCurveOptimizerAll']"
              @click="applySetting('SetCurveOptimizerAll', smuData.CurveOptimizerAll)"
            >Apply</a-button>
          </div>
        </div>

        <div class="grid grid-cols-2 gap-3">
          <div v-for="(_, index) in perCoreCurve" :key="index" 
          class="bg-white/5 p-3 rounded-xl border border-white/5 flex items-center justify-between">
            <span class="text-[10px] font-bold text-gray-500 uppercase">Core {{ index }}</span>
            <div class="flex items-center gap-2">
              <a-input-number
                v-model="perCoreCurve[index]"
                :min="-50" :max="50"
                size="mini"
                class="!w-12 !bg-transparent !border-none !text-white p-0"
                hide-button
              />
              <button
                class="w-6 h-6 bg-orange-600/20 text-orange-500 hover:bg-orange-600 hover:text-white transition-colors rounded flex items-center justify-center text-[10px]"
                @click="applySetting('SetCurveOptimizerPerCore', index, perCoreCurve[index])"
              >✓</button>
            </div>
          </div>
        </div>
      </div>

      <!-- Per Core OC Clocks 卡片 -->
      <div class="break-inside-avoid bg-[#1A1B26]/60 border border-white/5 p-6 rounded-3xl shadow-xl">
        <h3 class="text-xs font-black text-blue-500 uppercase tracking-widest mb-6 border-l-4 border-blue-600 pl-3">
          Per Core OC Clocks
        </h3>
        <div class="grid grid-cols-2 gap-3">
          <div v-for="(_, index) in perCoreOcClk" :key="index" 
          class="bg-white/5 p-3 rounded-xl border border-white/5 flex items-center justify-between">
            <span class="text-[10px] font-bold text-gray-500 uppercase">Core {{ index }}</span>
            <div class="flex items-center gap-2">
              <a-input-number
                v-model="perCoreOcClk[index]"
                :min="0" :max="1000" :step="25"
                size="mini"
                class="!w-14 !bg-transparent !border-none !text-white p-0"
                hide-button
              />
              <button
                class="w-6 h-6 bg-blue-600/20 text-blue-500 hover:bg-blue-600 hover:text-white transition-colors rounded flex items-center justify-center text-[10px]"
                @click="applySetting('SetPerCoreOcClk', index, perCoreOcClk[index])"
              >✓</button>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
  <div v-else class="flex items-center justify-center h-full">
    <a-spin dot />
  </div>
</template>

<style scoped lang="scss">
:deep(.arco-slider-button) {
  width: 12px;
  height: 12px;
}
</style>