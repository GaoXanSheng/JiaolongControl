<script async setup lang="ts">
import {reactive, ref, watch} from 'vue';
import {Message} from "@arco-design/web-vue";
import {Config, type ConfigInterface, RyzenSmu} from "@/utils/bridge";

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
const config = await Config.GetConfig();
const smuData = ref<ConfigInterface['RyzenSumConfig'] & { [key: string]: number }>(config.Data.RyzenSumConfig);

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
  if (!(methodName in RyzenSmu)) {
    Message.error(`Method ${methodName} is not defined`);
    return;
  }

  loadingMap[methodName] = true;
  try {
    const fn = RyzenSmu[methodName] as (...args: any[]) => Promise<any>;
    const res = await fn(...args);

    if (res && res.Success !== undefined) {
      res.Success ? Message.success(res.Message || 'Success') : Message.error(res.Message || 'Failed');
    } else {
      Message.success('Command executed successfully');
    }
  } catch (e) {
    Message.error('Execution failed');
    console.error(e);
  } finally {
    config.Data.RyzenSumConfig = smuData.value;
    await Config.SetConfig(config.Data);
    loadingMap[methodName] = false;
  }
};
</script>

<template>
  <a-layout class="smu-container">
    <a-layout-content class="content-body">

      <!-- 瀑布流容器 -->
      <div class="masonry-layout">

        <!-- 动态生成的基础配置卡片 -->
        <div class="masonry-item" v-for="group in CONFIG_GROUPS" :key="group.title">
          <a-card :title="group.title" size="small" class="group-card">
            <div v-for="item in group.items" :key="item.key" class="control-row">
              <div class="info">
                <span class="label">{{ item.label }}</span>
                <span class="value">{{ smuData[item.key] }}{{ item.unit }}</span>
              </div>
              <div class="actions">
                <a-slider
                    v-model="smuData[item.key]"
                    :min="item.min"
                    :max="item.max"
                    :step="item.step || 1"
                    class="slider"
                />
                <a-input-number
                    v-model="smuData[item.key]"
                    :min="item.min"
                    :max="item.max"
                    size="small"
                    style="width: 80px"
                    hide-button
                />
                <a-button
                    type="primary"
                    size="small"
                    :loading="loadingMap[item.key]"
                    @click="applySetting(item.key as keyof typeof RyzenSmu, smuData[item.key])"
                >Apply
                </a-button>
              </div>
            </div>

            <div v-if="group.title === 'Clocks & OC'" class="oc-actions">
              <a-button
                  type="primary"
                  status="success"
                  :loading="loadingMap['EnableOc']"
                  @click="applySetting('EnableOc')"
              >
                Enable OC
              </a-button>
              <a-button
                  type="primary"
                  status="danger"
                  :loading="loadingMap['DisableOc']"
                  @click="applySetting('DisableOc')"
              >
                Disable OC
              </a-button>
            </div>
          </a-card>
        </div>

        <!-- Curve Optimizer 卡片 -->
        <div class="masonry-item">
          <a-card title="Curve Optimizer" size="small" class="group-card">
            <template #extra>
              <div style="display: flex; align-items: center; gap: 8px;">
                <span style="font-size: 12px; color: var(--color-text-3)">Core Count:</span>
                <a-input-number
                    v-model="coreCount"
                    :min="1"
                    :max="64"
                    size="mini"
                    style="width: 60px"
                />
              </div>
            </template>
            <div class="control-row global-opt">
              <span class="label">All Core Offset</span>
              <div class="actions">
                <a-input-number v-model="smuData.SetCurveOptimizerAll" :min="-50" :max="50" size="small"/>
                <a-button
                    type="primary"
                    size="small"
                    :loading="loadingMap['SetCurveOptimizerAll']"
                    @click="applySetting('SetCurveOptimizerAll', smuData.SetCurveOptimizerAll)"
                >Apply
                </a-button>
              </div>
            </div>
            <a-divider style="margin: 12px 0;"/>
            <div class="per-core-grid">
              <div v-for="(_, index) in perCoreCurve" :key="index" class="core-item">
                <span class="core-label">Core {{ index }}</span>
                <a-input-number
                    v-model="perCoreCurve[index]"
                    :min="-50"
                    :max="50"
                    size="small"
                    style="width: 70px"
                    hide-button
                />
                <a-button
                    type="primary"
                    size="mini"
                    :loading="loadingMap['SetCurveOptimizerPerCore']"
                    @click="applySetting('SetCurveOptimizerPerCore', index, perCoreCurve[index])"
                >
                  <template #icon>✓</template>
                </a-button>
              </div>
            </div>
          </a-card>
        </div>

        <!-- Per Core OC Clocks 卡片 -->
        <div class="masonry-item">
          <a-card title="Per Core OC Clocks" size="small" class="group-card">
            <template #extra>
              <div style="display: flex; align-items: center; gap: 8px;">
                <span style="font-size: 12px; color: var(--color-text-3)">MHz</span>
              </div>
            </template>
            <div class="per-core-grid">
              <div v-for="(_, index) in perCoreOcClk" :key="index" class="core-item">
                <span class="core-label">Core {{ index }}</span>
                <a-input-number
                    v-model="perCoreOcClk[index]"
                    :min="0"
                    :max="1000"
                    :step="25"
                    size="small"
                    style="width: 70px"
                    hide-button
                />
                <a-button
                    type="primary"
                    size="mini"
                    :loading="loadingMap['SetPerCoreOcClk']"
                    @click="applySetting('SetPerCoreOcClk', index, perCoreOcClk[index])"
                >
                  <template #icon>✓</template>
                </a-button>
              </div>
            </div>
          </a-card>
        </div>

      </div>
    </a-layout-content>
  </a-layout>
</template>

<style scoped lang="scss">
.smu-container {
  .content-body {
    padding: 16px;
  }

  /* 瀑布流核心布局 */

  .masonry-layout {
    column-count: 2;
    column-gap: 24px;
    width: 100%;
  }

  /* 瀑布流子项 */

  .masonry-item {
    break-inside: avoid;
    margin-bottom: 24px;
    transform: translateZ(0);
  }

  .group-card {
    width: 100%;
  }

  .control-row {
    margin-bottom: 5px;

    .info {
      display: flex;
      justify-content: space-between;
      margin-bottom: 5px;
      font-size: 13px;

      .label {
        color: var(--color-text-2);
      }

      .value {
        color: var(--color-primary-light-4);
        font-weight: bold;
      }
    }

    .actions {
      display: flex;
      align-items: center;
      gap: 12px;

      .slider {
        flex: 1;
      }
    }
  }

  .oc-actions {
    display: flex;
    justify-content: center;
    gap: 16px;
    margin-top: 16px;
    padding-top: 16px;
    border-top: 1px solid var(--color-border-1);
  }

  .global-opt {
    display: flex;
    justify-content: space-between;
    align-items: center;
    background: var(--color-fill-2);
    padding: 8px 12px;
    border-radius: 4px;
  }

  .per-core-grid {
    display: grid;
    grid-template-columns: repeat(2, 1fr);
    gap: 8px;

    .core-item {
      display: flex;
      align-items: center;
      justify-content: space-between;
      background: var(--color-fill-1);
      padding: 4px 8px;
      border-radius: 4px;
      border: 1px solid var(--color-border-1);

      .core-label {
        font-size: 12px;
        color: var(--color-text-3);
      }
    }
  }
}
</style>