<script setup lang="ts">
import { reactive, ref, watch, computed, onMounted, onUnmounted } from 'vue';
import { Message } from "@arco-design/web-vue";
import { CPU, RyzenSmu } from "@/utils/bridge";
import { useConfigStore } from '@/stores/config';

const CONFIG_GROUPS = [
  {
    title: '功耗限制 (Power Limits)',
    items: [
      {label: 'STAPM 长期功耗上限', key: 'StapmLimit', min: 0, max: 200, unit: 'W', sliderClass: 'slider-purple'},
      {label: 'STAPM 时间窗口', key: 'StapmTime', min: 0, max: 3600, unit: 's', sliderClass: 'slider-purple'},
      {label: 'Fast 瞬时功耗上限', key: 'FastLimit', min: 0, max: 200, unit: 'W', sliderClass: 'slider-purple'},
      {label: 'Slow 持续功耗上限', key: 'SlowLimit', min: 0, max: 200, unit: 'W', sliderClass: 'slider-purple'},
      {label: 'Slow 功耗时间窗口', key: 'SlowTime', min: 0, max: 3600, unit: 's', sliderClass: 'slider-purple'},
      {label: 'PPT 功耗限制 (RSMU)', key: 'PptLimitRsmu', min: 0, max: 200, unit: 'W', sliderClass: 'slider-purple'},
    ]
  },
  {
    title: '电流限制 (Current Limits)',
    items: [
      {label: 'VRM 持续电流限制 (MP1)', key: 'VrmCurrentMp1', min: 0, max: 300000, step: 1000, unit: 'mA', sliderClass: 'slider-blue'},
      {label: 'VRM 持续电流限制 (RSMU)', key: 'VrmCurrentRsmu', min: 0, max: 300000, step: 1000, unit: 'mA', sliderClass: 'slider-blue'},
      {label: 'EDC 瞬间电流限制 (MP1)', key: 'EdcLimitMp1', min: 0, max: 300000, step: 1000, unit: 'mA', sliderClass: 'slider-blue'},
      {label: 'EDC 瞬间电流限制 (RSMU)', key: 'EdcLimitRsmu', min: 0, max: 300000, step: 1000, unit: 'mA', sliderClass: 'slider-blue'},
    ]
  },
  {
    title: '温度控制 (Thermal Control)',
    items: [
      {label: '温度墙限制 (MP1)', key: 'TempLimitMp1', min: 40, max: 115, unit: '℃', sliderClass: 'slider-red'},
      {label: '温度墙限制 (RSMU)', key: 'TempLimitRsmu', min: 40, max: 115, unit: '℃', sliderClass: 'slider-red'},
    ]
  },
  {
    title: '时钟与超频 (Clocks & OC)',
    items: [
      {label: 'PBO 倍率上限选择', key: 'PboScalar', min: 1, max: 100, unit: 'x', sliderClass: 'slider-purple'},
      {label: '超频核心频率偏移', key: 'OcClk', min: -500, max: 500, step: 25, unit: 'MHz', sliderClass: 'slider-purple'},
      {label: '超频核心电压设定', key: 'OcVolt', min: 0, max: 1550, step: 5, unit: 'mV', sliderClass: 'slider-purple'},
    ]
  }
];

const loadingMap = reactive<Record<string, boolean>>({});
const configStore = useConfigStore();
if (!configStore.config) {
  await configStore.fetchConfig();
}
const smuData = computed(() => configStore.config?.Smu);

// Physical core count fetched from backend (excludes hyperthreading)
const coreCount = ref(0);
const cpuName = ref('AMD Ryzen');
const cpuCoreInfo = ref('');

const perCoreCurve = reactive<number[]>([]);
const perCoreOcClk = reactive<number[]>([]);

watch(coreCount, (newCount) => {
  if (newCount <= 0) return;
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
      res.Success ? Message.success(res.Message || '应用成功') : Message.error(res.Message || '应用失败');
    } else {
      Message.success('命令应用成功');
    }
    configStore.debouncedSave();
  } catch (e) {
    Message.error('应用执行失败');
    console.error(e);
  } finally {
    loadingMap[methodName] = false;
  }
};

// ====== Real-time SMU Telemetry ======
const HISTORY_LEN = 24;
const telemetry = ref({ Ppt: 0, Tdc: 0, Edc: 0, Temp: 0, FreqMhz: 0, Usage: 0 });
const pptHistory = ref<number[]>(Array(HISTORY_LEN).fill(0));
const tdcHistory = ref<number[]>(Array(HISTORY_LEN).fill(0));
const edcHistory = ref<number[]>(Array(HISTORY_LEN).fill(0));
const tempHistory = ref<number[]>(Array(HISTORY_LEN).fill(0));

function pushHistory(arr: number[], value: number) {
  arr.push(value);
  if (arr.length > HISTORY_LEN) arr.shift();
}

function sparkline(history: number[], yMax: number): { line: string; area: string } {
  if (history.length < 2) return { line: 'M 0 40', area: 'M 0 40 L 160 40 L 0 40 Z' };
  const W = 160, H = 40;
  const points = history.map((v, i) => ({
    x: (i / (HISTORY_LEN - 1)) * W,
    y: H - (Math.max(0, Math.min(v, yMax)) / yMax) * H,
  }));
  const line = points.map((p, i) => {
    if (i === 0) return `M ${p.x},${p.y}`;
    const prev = points[i - 1];
    const cpx = (prev!.x + p.x) / 2;
    return `C ${cpx},${prev!.y} ${cpx},${p.y} ${p.x},${p.y}`;
  }).join(' ');
  const area = `${line} L ${W},${H} L 0,${H} Z`;
  return { line, area };
}

const pptChart = computed(() => sparkline(pptHistory.value, 150));
const tdcChart = computed(() => sparkline(tdcHistory.value, 300));
const edcChart = computed(() => sparkline(edcHistory.value, 400));
const tempChart = computed(() => sparkline(tempHistory.value, 110));

let pollingTimer: ReturnType<typeof setInterval> | null = null;

async function fetchTelemetry() {
  try {
    const res = await RyzenSmu.GetSmuTelemetry();
    if (res.Success && res.Data) {
      telemetry.value = res.Data;
      pushHistory(pptHistory.value, res.Data.Ppt);
      pushHistory(tdcHistory.value, res.Data.Tdc);
      pushHistory(edcHistory.value, res.Data.Edc);
      pushHistory(tempHistory.value, res.Data.Temp);
    }
  } catch (e) {
    // silent fail — telemetry is best-effort
  }
}

onMounted(async () => {
  // Fetch real physical core count (no hyperthreading)
  try {
    const coreRes = await CPU.GetPhysicalCoreCount();
    if (coreRes.Success && coreRes.Data > 0) {
      coreCount.value = coreRes.Data;
    } else {
      coreCount.value = 8; // safe fallback
    }
  } catch {
    coreCount.value = 8;
  }

  // Fetch CPU name for display
  try {
    const infoRes = await CPU.GetCpuInfo();
    if (infoRes.Success && infoRes.Data) {
      cpuName.value = infoRes.Data.Name || 'AMD Ryzen';
      cpuCoreInfo.value = `${infoRes.Data.Cores} 核心 / ${infoRes.Data.Threads} 线程`;
    }
  } catch { /* ignore */ }

  fetchTelemetry();
  pollingTimer = setInterval(fetchTelemetry, 3000);
});

onUnmounted(() => {
  if (pollingTimer) clearInterval(pollingTimer);
});
</script>

<template>
  <div class="h-full overflow-y-auto text-white p-6 no-scrollbar" v-if="smuData">
    <div class="max-w-[1300px] mx-auto flex flex-col lg:flex-row gap-6">

      <!-- ==================== 左/中：高级电源、频率微调区 ==================== -->
      <div class="flex-1 space-y-6">
        <!-- 头部标题 -->
        <div>
          <h1 class="text-2xl font-bold tracking-wide">Ryzen SMU</h1>
          <p class="text-[13px] text-gray-500 mt-1">高级电源、电流及频率限制调整 (AMD Ryzen 平台专用)</p>
        </div>

        <div class="grid grid-cols-1 md:grid-cols-2 gap-5">
          <!-- 动态生成的配置卡片（分成左右两个大组排布更整齐） -->
          <div v-for="group in CONFIG_GROUPS" :key="group.title"
               class="bg-[#121320]/60 backdrop-blur-md border border-white/[0.05] rounded-xl p-5 shadow-lg flex flex-col justify-between">
            <div>
              <h3 class="text-xs font-black text-purple-400 uppercase tracking-widest mb-5 border-l-4 border-purple-600 pl-2.5">
                {{ group.title }}
              </h3>

              <div class="space-y-5">
                <div v-for="item in group.items" :key="item.key" class="space-y-1.5">
                  <div class="flex justify-between items-center text-[11px]">
                    <span class="text-gray-400">{{ item.label }}</span>
                    <span class="text-white font-mono font-medium">{{ smuData[item.key] }} {{ item.unit }}</span>
                  </div>
                  <div class="flex items-center gap-4">
                    <a-slider
                        v-model="smuData[item.key]"
                        :min="item.min"
                        :max="item.max"
                        :step="item.step || 1"
                        class="flex-1"
                        :class="item.sliderClass"
                    />
                    <a-button
                        type="primary"
                        size="small"
                        class="!bg-purple-600/10 !text-purple-400 !border-purple-500/20 hover:!bg-purple-600 hover:!text-white rounded-md px-3 font-semibold transition"
                        :loading="loadingMap[item.key]"
                        @click="applySetting(('Set' + item.key) as keyof typeof RyzenSmu, smuData[item.key])"
                    >应用</a-button>
                  </div>
                </div>
              </div>
            </div>

            <!-- 仅在时钟与超频面板底部显示 OC 开关 -->
            <div v-if="group.title.includes('Clocks')" class="mt-6 flex gap-3 pt-5 border-t border-white/[0.03]">
              <a-button
                  type="primary"
                  class="flex-1 !rounded-lg font-bold !bg-emerald-600/20 !text-emerald-400 !border-emerald-500/20 hover:!bg-emerald-600 hover:!text-white"
                  :loading="loadingMap['EnableOc']"
                  @click="applySetting('EnableOc')"
              >启用超频</a-button>
              <a-button
                  type="primary"
                  class="flex-1 !rounded-lg font-bold !bg-rose-600/20 !text-rose-400 !border-rose-500/20 hover:!bg-rose-600 hover:!text-white"
                  :loading="loadingMap['DisableOc']"
                  @click="applySetting('DisableOc')"
              >禁用超频</a-button>
            </div>
          </div>
        </div>

        <!-- 下部分割栏（Curve Optimizer 与 单核超频） -->
        <div class="grid grid-cols-1 md:grid-cols-2 gap-5">

          <!-- Curve Optimizer 面板 -->
          <div class="bg-[#121320]/60 backdrop-blur-md border border-white/[0.05] rounded-xl p-5 shadow-lg flex flex-col justify-between">
            <div>
              <div class="flex justify-between items-center mb-4">
                <h3 class="text-xs font-black text-orange-400 uppercase tracking-widest border-l-4 border-orange-500 pl-2.5">
                  Curve Optimizer 曲线优化
                </h3>
                <div class="flex items-center gap-2">
                  <span class="text-[9px] font-bold text-gray-500 uppercase">Cores</span>
                  <a-input-number
                      v-model="coreCount"
                      :min="1" :max="64"
                      size="mini"
                      class="!w-12 !bg-white/5 !border-white/10 !text-white rounded-md"
                      hide-button
                  />
                </div>
              </div>

              <!-- 全核偏移调节块 -->
              <div class="bg-white/[0.02] border border-white/[0.04] p-3.5 rounded-lg mb-4">
                <div class="flex justify-between items-center mb-1 text-[11px]">
                  <span class="font-bold text-gray-300">All Core Offset (全核心偏移量)</span>
                  <span class="font-mono text-orange-400 font-semibold">{{ smuData.CurveOptimizerAll }}</span>
                </div>
                <div class="flex items-center gap-4">
                  <a-slider v-model="smuData.CurveOptimizerAll" :min="-100" :max="100" class="flex-1 slider-orange" />
                  <a-button
                      type="primary"
                      size="small"
                      class="!bg-orange-600/10 !text-orange-400 !border-orange-500/25 hover:!bg-orange-600 hover:!text-white rounded-md px-3 font-semibold transition"
                      :loading="loadingMap['SetCurveOptimizerAll']"
                      @click="applySetting('SetCurveOptimizerAll', smuData.CurveOptimizerAll)"
                  >应用</a-button>
                </div>
              </div>

              <!-- 单核优化矩阵 -->
              <div class="grid grid-cols-2 gap-2 max-h-[160px] overflow-y-auto no-scrollbar">
                <div v-for="(_, index) in perCoreCurve" :key="index"
                     class="bg-white/[0.02] p-2.5 rounded-lg border border-white/[0.03] flex items-center justify-between">
                  <span class="text-[9px] font-bold text-gray-500 uppercase">CORE {{ index }}</span>
                  <div class="flex items-center gap-1.5">
                    <a-input-number
                        v-model="perCoreCurve[index]"
                        :min="-50" :max="50"
                        size="mini"
                        class="!w-10 !bg-transparent !border-none !text-white p-0 text-center font-mono"
                        hide-button
                    />
                    <button
                        class="w-5 h-5 bg-orange-600/10 text-orange-400 hover:bg-orange-600 hover:text-white transition-colors border border-orange-500/20 rounded flex items-center justify-center text-[10px]"
                        @click="applySetting('SetCurveOptimizerPerCore', index, perCoreCurve[index])"
                    >✓</button>
                  </div>
                </div>
              </div>
            </div>
          </div>

          <!-- Per Core OC Clocks 面板 -->
          <div class="bg-[#121320]/60 backdrop-blur-md border border-white/[0.05] rounded-xl p-5 shadow-lg flex flex-col justify-between">
            <div>
              <h3 class="text-xs font-black text-blue-400 uppercase tracking-widest mb-4 border-l-4 border-blue-500 pl-2.5">
                Per Core OC Clocks (单核超频限制)
              </h3>

              <div class="grid grid-cols-2 gap-2 max-h-[240px] overflow-y-auto no-scrollbar">
                <div v-for="(_, index) in perCoreOcClk" :key="index"
                     class="bg-white/[0.02] p-2.5 rounded-lg border border-white/[0.03] flex items-center justify-between">
                  <span class="text-[9px] font-bold text-gray-500 uppercase">CORE {{ index }}</span>
                  <div class="flex items-center gap-1.5">
                    <a-input-number
                        v-model="perCoreOcClk[index]"
                        :min="0" :max="1000" :step="25"
                        size="mini"
                        class="!w-12 !bg-transparent !border-none !text-white p-0 text-center font-mono"
                        hide-button
                    />
                    <button
                        class="w-5 h-5 bg-blue-600/10 text-blue-400 hover:bg-blue-600 hover:text-white transition-colors border border-blue-500/20 rounded flex items-center justify-center text-[10px]"
                        @click="applySetting('SetPerCoreOcClk', index, perCoreOcClk[index])"
                    >✓</button>
                  </div>
                </div>
              </div>
            </div>
          </div>

        </div>
      </div>

      <!-- ==================== 右侧：处理器信息与电源遥测栏 ==================== -->
      <div class="w-full lg:w-[360px] shrink-0 space-y-6 lg:pt-[115px]">

        <!-- 1. AMD Ryzen 处理器芯片详情 -->
        <div class="bg-[#121320]/60 backdrop-blur-md border border-white/[0.05] rounded-xl p-5 shadow-lg">
          <h2 class="text-[13px] font-semibold text-gray-300 mb-4">Ryzen 芯片架构</h2>
          <div class="flex items-center gap-4">
            <!-- AM5 Socket 异形芯片 SVG 绘制 -->
            <div class="w-16 h-16 bg-white/[0.02] border border-white/[0.05] rounded-xl flex items-center justify-center relative shrink-0">
              <svg t="1784304129102" class="icon" viewBox="0 0 1024 1024" version="1.1" xmlns="http://www.w3.org/2000/svg" p-id="16169" width="200" height="200"><path d="M661.205333 130.005333c33.92-4.352 33.92-4.352 33.92 29.269334 0 9.642667-0.853333 19.413333 0.384 28.885333 0.597333 4.778667 5.674667 8.832 7.509334 13.653333 2.986667 7.978667 4.906667 16.341333 8.149333 27.434667 21.418667 1.706667 47.36-2.901333 67.328 16.64 20.266667 19.754667 17.792 45.013333 17.834667 70.912l15.786666 0.981333c2.986667 0.213333 8.106667-0.426667 8.746667 0.938667 10.410667 21.76 29.866667 9.301333 45.141333 11.861333 9.344 1.578667 19.157333 0.298667 29.866667 0.298667v36.906667c-18.090667 0-35.84-0.682667-53.504 0.341333-6.528 0.384-12.714667 5.12-19.285333 7.082667-5.12 1.536-10.709333 2.389333-16.085334 2.389333-7.722667-0.085333-11.050667 2.816-10.794666 10.752 0.426667 11.52 0.085333 23.082667 0.085333 35.84 6.528 0.341333 11.477333 0.554667 16.384 0.853333 2.986667 0.256 8.192-0.170667 8.704 1.109334 8.149333 20.522667 25.728 9.557333 39.04 11.477333 11.349333 1.621333 23.082667 0.341333 35.584 0.341333v37.290667c-19.968 0-39.210667-0.512-58.368 0.341333-4.565333 0.170667-8.874667 5.034667-13.525333 7.253334-4.096 2.005333-8.746667 5.034667-12.842667 4.650666-12.586667-1.152-16.128 4.181333-15.189333 15.786667 0.853333 9.984 0.170667 20.138667 0.170666 31.786667 6.698667 0.341333 12.330667-0.170667 17.408 1.109333 3.968 1.024 7.210667 4.522667 10.965334 6.528 4.053333 2.133333 8.32 5.12 12.544 5.290667 19.2 0.554667 38.528 0.256 58.752 0.256v37.290666c-18.517333 0-36.693333-0.213333-54.826667 0.085334-7.04 0.085333-16-2.986667-17.621333 8.96-0.256 1.493333-5.674667 2.858667-8.789334 3.157333-5.418667 0.512-10.965333 0.128-18.346666 0.128 0 14.122667-0.768 26.752 0.426666 39.168 0.384 3.242667 6.4 7.466667 10.538667 8.490667 7.253333 1.792 16.810667-2.56 18.517333 10.24 0.213333 1.450667 8.021333 2.645333 12.330667 2.730666 18.773333 0.298667 37.546667 0.128 57.514667 0.128v37.077334c-18.218667 0-36.352-0.554667-54.4 0.341333-4.693333 0.213333-9.045333 5.248-13.866667 7.338667-5.034667 2.218667-10.453333 4.906667-15.744 4.949333-9.045333 0.042667-13.226667 2.858667-12.373333 12.330667 0.512 6.016 0.256 12.16 0 18.218666-1.536 34.090667-24.448 56.533333-58.666667 57.514667-4.565333 0.128-9.173333 0.469333-13.653333 0-11.349333-1.194667-17.152 2.005333-16.298667 14.890667 0.298667 4.48-4.778667 9.258667-7.125333 14.037333-2.005333 4.138667-4.992 8.448-5.12 12.757333-0.554667 18.645333-0.256 37.376-0.256 57.344h-36.778667v-55.04c0-6.997333 0-12.202667-8.234667-17.152-5.376-3.2-5.12-15.786667-7.722666-25.557333h-44.8v15.786667c-0.085333 2.517333 0.469333 6.613333-0.725334 7.296-23.04 12.8-8.064 34.261333-11.818666 51.285333-1.578667 7.168-0.298667 14.933333-0.298667 23.552h-37.077333v-55.381333c-0.042667-6.826667 1.578667-14.08-8.746667-16.554667-2.389333-0.597333-3.84-9.472-3.797333-14.549333 0.085333-8.96-2.986667-13.354667-12.373334-12.672-5.034667 0.384-10.24 0.469333-15.232-0.042667-14.208-1.450667-24.448 1.024-21.077333 18.944 0.341333 1.877333 0.213333 5.290667-0.810667 5.802667-20.352 9.856-9.002667 28.16-11.178666 42.581333-1.493333 10.282667-0.256 20.992-0.256 32.256H439.466667c0-18.389333 0.128-36.437333-0.085334-54.442667-0.085333-7.04 1.749333-13.653333-8.362666-18.517333-4.778667-2.304-4.096-16-6.314667-26.453333-11.818667 0-24.533333-0.768-36.992 0.512-3.285333 0.341333-7.68 6.528-8.533333 10.624-1.450667 7.552 1.706667 16.298667-9.984 19.029333-1.877333 0.426667-2.901333 7.893333-2.986667 12.117333-0.298667 18.688-0.128 37.333333-0.128 56.917334h-36.949333c0-18.261333 0.512-36.309333-0.384-54.314667-0.213333-4.608-5.162667-8.874667-7.68-13.44-1.792-3.157333-4.053333-6.442667-4.522667-9.898667-0.768-5.376-0.213333-10.88-0.213333-17.493333-25.685333-2.304-51.626667 2.56-71.637334-17.706667-19.413333-19.754667-16.213333-44.586667-17.152-70.016-6.229333-0.384-11.648-0.426667-16.938666-1.109333-3.328-0.426667-9.088-1.408-9.301334-2.858667-2.346667-13.994667-12.970667-10.197333-21.376-10.325333-16.725333-0.298667-33.493333-0.085333-51.370666-0.085333v-36.693334c19.2 0 37.888 0.597333 56.533333-0.341333 5.12-0.256 9.813333-5.589333 15.061333-7.808a46.933333 46.933333 0 0 1 16.213334-4.522667c8.192-0.213333 12.117333-2.56 11.733333-11.434666-0.554667-11.52-0.128-23.04-0.128-35.626667-6.698667-0.341333-11.605333-0.597333-16.554667-0.938667-2.986667-0.213333-8.192 0.213333-8.704-1.109333-7.936-19.925333-24.917333-9.429333-37.888-11.093333-11.477333-1.493333-23.296-0.341333-36.181333-0.341334v-37.034666l57.173333-0.042667c6.4 0 13.056 1.450667 15.104-8.362667 0.469333-2.261333 9.386667-4.138667 14.250667-3.882666 10.24 0.512 13.525333-3.584 12.970667-13.354667-0.682667-11.008-0.170667-22.058667-0.170667-35.114667-7.082667 0-12.544 0.170667-17.92-0.085333-2.986667-0.128-8.277333-0.554667-8.448-1.621333-3.242667-16.938667-16.554667-9.770667-25.728-10.325334-15.573333-0.981333-31.274667-0.256-47.616-0.256v-37.461333c19.498667 0 38.229333 0.597333 56.874667-0.384 5.12-0.256 9.770667-5.888 15.061333-7.68 8.192-2.773333 16.768-4.352 27.690667-7.04 0-11.733333 0.597333-24.746667-0.512-37.546667-0.256-2.901333-6.229333-6.613333-10.154667-7.509333-7.253333-1.706667-16.554667 2.602667-18.986667-9.770667-0.426667-1.92-8.96-3.157333-13.738666-3.242666-18.688-0.384-37.418667-0.170667-56.490667-0.170667v-36.906667c18.773333 0 37.034667 0.085333 55.253333-0.042666 7.338667-0.042667 14.122667 1.152 18.218667-9.130667 1.621333-4.181333 13.952-4.096 23.125333-6.4 0.853333-23.509333-2.56-49.194667 17.621334-68.906667 19.669333-19.2 44.714667-16.213333 69.802666-16.810666l1.066667-14.464c0.213333-2.986667-0.597333-7.978667 0.853333-8.789334 21.461333-11.946667 10.794667-32 11.605334-48.341333 1.536-32.213333 2.048-33.493333 37.290666-26.538667v53.888c0.085333 7.253333-0.981333 13.44 8.874667 18.133334 4.906667 2.304 4.437333 15.957333 6.613333 25.429333h44.16c3.328-11.648-4.352-26.24 12.714667-31.402667 1.834667-0.554667 1.066667-10.453333 1.152-16 0.128-12.672 0.682667-25.429333-0.170667-38.058666-0.853333-11.733333 3.925333-15.488 15.018667-14.378667 6.485333 0.682667 13.056 0.128 22.314667 0.128 0 20.181333-0.128 39.210667 0 58.24 0.085333 6.272-0.341333 11.136 7.936 15.488 5.162667 2.688 4.906667 15.744 7.424 25.6h44.757333l0.981333-14.933333c0.170667-2.56-0.554667-6.4 0.768-7.381334 22.954667-16.64 9.386667-40.405333 10.837334-60.586666 1.109333-15.488 7.04-16.981333 19.456-16.64 11.52 0.256 21.461333-0.597333 19.413333 16.213333-1.450667 12.032-0.64 24.362667-0.128 36.522667 0.341333 8.106667-3.968 17.621333 9.472 22.186666 3.669333 1.237333 2.944 15.488 4.394667 24.832h46.634666c0-6.186667-0.213333-11.605333 0.128-17.066666 0.170667-2.858667 0.896-7.936 2.218667-8.192 15.274667-3.328 9.898667-15.317333 10.24-24.277334 0.597333-16.085333 0.170667-32.170667 0.170667-46.762666 2.176-1.792 2.56-2.346667 2.986666-2.389334zM311.722667 284.586667c-23.082667 0-28.714667 5.717333-28.714667 28.928v398.762666c0 24.277333 5.376 29.738667 29.525333 29.781334h398.378667c23.210667 0 28.458667-5.546667 28.501333-29.013334 0.085333-66.474667 0.042667-132.906667 0.042667-199.381333V312.746667c-0.042667-22.272-5.717333-28.117333-27.818667-28.16H311.722667z m27.477333 37.930666c115.541333 0.597333 231.082667 0.469333 346.624 0.085334 12.885333-0.042667 17.749333 2.901333 17.706667 16.810666-0.512 116.224-0.426667 232.490667-0.085334 348.714667 0.042667 11.648-2.986667 16-15.36 15.957333-117.077333-0.426667-234.112-0.426667-351.146666 0-13.056 0.085333-16.256-4.522667-16.128-16.768 0.128-14.549333 0.170667-29.141333 0.213333-43.733333 0.170667-43.776 0-87.552 0-131.328 0-49.749333 0.298667-99.498667-0.128-149.248 0-7.125333-0.085333-14.208-0.170667-21.333333-0.213333-14.165333 3.285333-19.242667 18.474667-19.2z m54.656 45.525334a28.885333 28.885333 0 0 0-28.501333 29.184 27.349333 27.349333 0 0 0 27.989333 27.690666 28.757333 28.757333 0 0 0 29.013333-28.842666 29.226667 29.226667 0 0 0-28.501333-28.032z" fill="#828796" p-id="16170"></path></svg>
            </div>

            <div class="space-y-1 text-[11px] text-gray-400">
              <div class="text-[13px] font-bold text-white">
                <span v-if="cpuName">{{ cpuName }}</span>
                <span v-else class="text-gray-600 animate-pulse">检测中...</span>
              </div>
              <div>AMD Ryzen 架构 / AM5 接口</div>
              <div>
                <span v-if="cpuCoreInfo">{{ cpuCoreInfo }}</span>
                <span v-else class="text-gray-600 animate-pulse">{{ coreCount > 0 ? `${coreCount} 物理核心` : '检测中...' }}</span>
              </div>
              <div>Curve Optimizer 已加载 {{ coreCount }} 核</div>
              <div>支持 PBO2 曲线优化</div>
            </div>
          </div>
        </div>

        <!-- 2. 电源实时监视器（遥测 PPT / TDC / EDC 波形图） -->
        <div class="bg-[#121320]/60 backdrop-blur-md border border-white/[0.05] rounded-xl p-5 shadow-lg space-y-4">
          <div class="flex items-center justify-between">
            <h2 class="text-[13px] font-semibold text-gray-300">SMU 电源遥测</h2>
            <span class="text-[10px] text-gray-600 bg-white/[0.03] border border-white/[0.05] px-2 py-0.5 rounded-full">{{ telemetry.FreqMhz }} MHz · {{ telemetry.Usage }}% 负载</span>
          </div>

          <div class="grid grid-cols-2 gap-3">
            <!-- PPT 功耗 -->
            <div class="bg-white/[0.02] border border-white/[0.04] p-3 rounded-lg flex flex-col justify-between">
              <div>
                <span class="text-[10px] text-gray-500 block">PPT 封装功耗</span>
                <span class="text-base font-bold text-white font-mono">{{ telemetry.Ppt.toFixed(1) }} <span class="text-[10px] text-gray-500 font-bold">W</span></span>
              </div>
              <svg class="w-full h-8 opacity-80 mt-1" viewBox="0 0 160 40" preserveAspectRatio="none">
                <defs>
                  <linearGradient id="smu-g-purple" x1="0" y1="0" x2="0" y2="1"><stop offset="0%" stop-color="#8A2BE2" stop-opacity="0.35" /><stop offset="100%" stop-color="#8A2BE2" stop-opacity="0" /></linearGradient>
                </defs>
                <path :d="pptChart.line" fill="none" stroke="#8A2BE2" stroke-width="1.5" stroke-linecap="round"/>
                <path :d="pptChart.area" fill="url(#smu-g-purple)" />
              </svg>
            </div>

            <!-- TDC 长期电流 -->
            <div class="bg-white/[0.02] border border-white/[0.04] p-3 rounded-lg flex flex-col justify-between">
              <div>
                <span class="text-[10px] text-gray-500 block">TDC 供电电流</span>
                <span class="text-base font-bold text-white font-mono">{{ telemetry.Tdc.toFixed(1) }} <span class="text-[10px] text-gray-500 font-bold">A</span></span>
              </div>
              <svg class="w-full h-8 opacity-80 mt-1" viewBox="0 0 160 40" preserveAspectRatio="none">
                <defs>
                  <linearGradient id="smu-g-blue" x1="0" y1="0" x2="0" y2="1"><stop offset="0%" stop-color="#3B82F6" stop-opacity="0.35" /><stop offset="100%" stop-color="#3B82F6" stop-opacity="0" /></linearGradient>
                </defs>
                <path :d="tdcChart.line" fill="none" stroke="#3B82F6" stroke-width="1.5" stroke-linecap="round"/>
                <path :d="tdcChart.area" fill="url(#smu-g-blue)" />
              </svg>
            </div>

            <!-- EDC 瞬间电流 -->
            <div class="bg-white/[0.02] border border-white/[0.04] p-3 rounded-lg flex flex-col justify-between">
              <div>
                <span class="text-[10px] text-gray-500 block">EDC 峰值电流</span>
                <span class="text-base font-bold text-white font-mono">{{ telemetry.Edc.toFixed(1) }} <span class="text-[10px] text-gray-500 font-bold">A</span></span>
              </div>
              <svg class="w-full h-8 opacity-80 mt-1" viewBox="0 0 160 40" preserveAspectRatio="none">
                <defs>
                  <linearGradient id="smu-g-orange" x1="0" y1="0" x2="0" y2="1"><stop offset="0%" stop-color="#FF7D00" stop-opacity="0.35" /><stop offset="100%" stop-color="#FF7D00" stop-opacity="0" /></linearGradient>
                </defs>
                <path :d="edcChart.line" fill="none" stroke="#FF7D00" stroke-width="1.5" stroke-linecap="round"/>
                <path :d="edcChart.area" fill="url(#smu-g-orange)" />
              </svg>
            </div>

            <!-- 核心温度 -->
            <div class="bg-white/[0.02] border border-white/[0.04] p-3 rounded-lg flex flex-col justify-between">
              <div>
                <span class="text-[10px] text-gray-500 block">核心温度</span>
                <span class="text-base font-bold font-mono" :class="telemetry.Temp > 90 ? 'text-red-400' : telemetry.Temp > 75 ? 'text-orange-400' : 'text-white'">{{ telemetry.Temp.toFixed(1) }} <span class="text-[10px] text-gray-500 font-bold">°C</span></span>
              </div>
              <svg class="w-full h-8 opacity-80 mt-1" viewBox="0 0 160 40" preserveAspectRatio="none">
                <defs>
                  <linearGradient id="smu-g-red" x1="0" y1="0" x2="0" y2="1"><stop offset="0%" stop-color="#EF4444" stop-opacity="0.35" /><stop offset="100%" stop-color="#EF4444" stop-opacity="0" /></linearGradient>
                </defs>
                <path :d="tempChart.line" fill="none" stroke="#EF4444" stroke-width="1.5" stroke-linecap="round"/>
                <path :d="tempChart.area" fill="url(#smu-g-red)" />
              </svg>
            </div>
          </div>
        </div>


        <!-- 3. 技术名释说明 -->
        <div class="bg-[#121320]/60 backdrop-blur-md border border-white/[0.05] rounded-xl p-5 shadow-lg space-y-2.5">
          <h2 class="text-[13px] font-semibold text-gray-300">名词解释</h2>
          <div class="text-[11px] text-gray-500 leading-relaxed space-y-2">
            <p><strong>STAPM</strong>: 根据设备表面温度自适应调整 CPU 功耗分配（在移动端设备和掌机上尤为明显）。</p>
            <p><strong>Curve Optimizer (PBO2)</strong>: 通过调校不同内核的电压频率曲线（降压超频），能实现在更低温度下达到更高运行频率的目标。</p>
            <p><strong>RSMU / MP1</strong>: 芯片内部不同模块的系统级微处理器，两者的限制参数相互协调限制。</p>
          </div>
          <a target="_blank" href="https://www.amd.com/zh-cn/developer/browse-by-resource-type/documentation.html" class="text-[11px] text-blue-400 hover:text-blue-300 cursor-pointer pt-1 flex items-center gap-0.5 font-medium transition-colors">
            参考 AMD PBO 手册
          </a>
        </div>

      </div>
    </div>
  </div>
  <div v-else class="flex items-center justify-center h-full">
    <a-spin dot />
  </div>
</template>

<style scoped lang="scss">
/* 隐藏自定义滚动条 */
.no-scrollbar::-webkit-scrollbar {
  display: none;
}
.no-scrollbar {
  -ms-overflow-style: none;
  scrollbar-width: none;
}

/* 统一高保真 Slider 拖拽钮 */
:deep(.arco-slider-button) {
  background-color: #ffffff !important;
  width: 12px !important;
  height: 12px !important;
  border-radius: 99px !important;
  box-shadow: 0 0 8px rgba(138, 43, 226, 0.6) !important;
}
:deep(.arco-slider-track) {
  background-color: rgba(255, 255, 255, 0.04) !important;
  height: 5px !important;
  border-radius: 99px;
}

/* 分色重写 Slider 轨道（紫 / 蓝 / 红 / 橘） */
:deep(.slider-purple .arco-slider-bar) {
  background: linear-gradient(90deg, #6366f1 0%, #8A2BE2 100%) !important;
  height: 5px !important;
  border-radius: 99px;
}
:deep(.slider-purple .arco-slider-button) {
  border: 2px solid #8A2BE2 !important;
  box-shadow: 0 0 8px rgba(138, 43, 226, 0.6) !important;
}

:deep(.slider-blue .arco-slider-bar) {
  background: linear-gradient(90deg, #3b82f6 0%, #1d4ed8 100%) !important;
  height: 5px !important;
  border-radius: 99px;
}
:deep(.slider-blue .arco-slider-button) {
  border: 2px solid #3b82f6 !important;
  box-shadow: 0 0 8px rgba(59, 130, 246, 0.6) !important;
}

:deep(.slider-red .arco-slider-bar) {
  background: linear-gradient(90deg, #f43f5e 0%, #e11d48 100%) !important;
  height: 5px !important;
  border-radius: 99px;
}
:deep(.slider-red .arco-slider-button) {
  border: 2px solid #e11d48 !important;
  box-shadow: 0 0 8px rgba(225, 29, 72, 0.6) !important;
}

:deep(.slider-orange .arco-slider-bar) {
  background: linear-gradient(90deg, #ff7d00 0%, #ff5000 100%) !important;
  height: 5px !important;
  border-radius: 99px;
}
:deep(.slider-orange .arco-slider-button) {
  border: 2px solid #ff7d00 !important;
  box-shadow: 0 0 8px rgba(255, 125, 0, 0.6) !important;
}

/* 深色模式下拉选择框 */
:deep(.arco-select-view-single) {
  background-color: #17192a !important;
  border: 1px solid rgba(255, 255, 255, 0.05) !important;
  color: #ffffff !important;
  border-radius: 6px !important;
  height: 28px !important;
}
</style>