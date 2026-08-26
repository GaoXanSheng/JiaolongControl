<template>
  <div class="h-full overflow-y-auto text-white p-6 no-scrollbar">
    <div class="max-w-[1300px] mx-auto space-y-6">
      <div>
        <h1 class="text-2xl font-bold tracking-wide">风扇曲线编辑器</h1>
        <p class="text-[13px] text-gray-500 mt-1">
          可视化拖动调整不同核心温度下的风扇转速，支持 CPU/GPU 独立配置。
        </p>
      </div>
      <a-card class="fan-curve-card" :bordered="false" @click="closeMenu">
        <div class="header-info">
          <!-- 左侧：CPU/GPU 切换及移除设置 -->
          <div class="info-section">
            <a-space size="medium">
              <a-radio-group
                v-model="activeTab"
                type="button"
                :class="['radio-group-dark', activeTab === 'GPU' ? 'radio-gpu' : '']"
                @change="onTabChange"
              >
                <a-radio value="CPU">CPU 曲线</a-radio>
                <a-radio value="GPU">GPU 曲线</a-radio>
              </a-radio-group>
              <button
                class="text-xs font-semibold text-rose-400 border border-rose-500/20 bg-rose-500/10 hover:bg-rose-500 hover:text-white px-4 py-1.5 rounded-lg transition-all"
                @click="handleRemoveFanClick"
              >
                移除转速设置
              </button>
            </a-space>
          </div>
          <div class="control-section">
            <a-space size="medium">
              <a-tag :color="isServiceRunning ? 'green' : 'gray'" bordered class="status-tag">
                <template #icon>
                  <div :class="['status-dot', { active: isServiceRunning }]"></div>
                </template>
                {{ isServiceRunning ? '运行中' : '已停止' }}
              </a-tag>

              <a-switch
                v-model="isServiceRunning"
                :loading="serviceLoading"
                :before-change="handleServiceToggle"
                class="switch-purple"
              />
            </a-space>
          </div>
          <div class="help-section sub-info">拖拽节点调整 / 右键管理节点</div>
        </div>
        <div ref="containerRef" class="svg-container">
          <svg
            v-if="isValidRender"
            :width="width"
            :height="height"
            @mousemove="onSvgMouseMove"
            @mouseup="onDragEnd"
            @mouseleave="onDragEnd"
          >
            <defs>
              <filter id="shadow" x="-50%" y="-50%" width="200%" height="200%">
                <feDropShadow
                  dx="0"
                  dy="0"
                  stdDeviation="3"
                  :flood-color="activeTab === 'CPU' ? '#8A2BE2' : '#10B981'"
                  flood-opacity="0.8"
                />
              </filter>
              <linearGradient id="cpu-glow" x1="0" y1="0" x2="0" y2="1">
                <stop offset="0%" stop-color="#8A2BE2" stop-opacity="0.25" />
                <stop offset="100%" stop-color="#8A2BE2" stop-opacity="0.0" />
              </linearGradient>
              <linearGradient id="gpu-glow" x1="0" y1="0" x2="0" y2="1">
                <stop offset="0%" stop-color="#10B981" stop-opacity="0.25" />
                <stop offset="100%" stop-color="#10B981" stop-opacity="0.0" />
              </linearGradient>
            </defs>

            <g class="grid">
              <line
                v-for="i in 11"
                :key="'v-' + i"
                :x1="
                  safeMapX(
                    currentTempRange[0]! +
                      ((i - 1) * (currentTempRange[1]! - currentTempRange[0]!)) / 10,
                  )
                "
                :y1="padding.top"
                :x2="
                  safeMapX(
                    currentTempRange[0]! +
                      ((i - 1) * (currentTempRange[1]! - currentTempRange[0]!)) / 10,
                  )
                "
                :y2="height - padding.bottom"
                stroke="rgba(255, 255, 255, 0.03)"
                stroke-dasharray="3"
              />
              <line
                v-for="i in 11"
                :key="'h-' + i"
                :x1="padding.left"
                :y1="safeMapY(speedRange[0]! + ((i - 1) * (speedRange[1]! - speedRange[0]!)) / 10)"
                :x2="width - padding.right"
                :y2="safeMapY(speedRange[0]! + ((i - 1) * (speedRange[1]! - speedRange[0]!)) / 10)"
                stroke="rgba(255, 255, 255, 0.03)"
                stroke-dasharray="3"
              />
            </g>

            <g class="labels" style="user-select: none; pointer-events: none">
              <text
                v-for="i in 6"
                :key="'xl-' + i"
                :x="
                  safeMapX(
                    currentTempRange[0]! +
                      ((i - 1) * (currentTempRange[1]! - currentTempRange[0]!)) / 5,
                  )
                "
                :y="height - 12"
                text-anchor="middle"
                fill="rgba(255, 255, 255, 0.3)"
                font-size="9"
                font-family="monospace"
              >
                {{
                  Math.round(
                    currentTempRange[0]! +
                      ((i - 1) * (currentTempRange[1]! - currentTempRange[0]!)) / 5,
                  )
                }}°C
              </text>
              <text
                v-for="i in 6"
                :key="'yl-' + i"
                :x="padding.left - 12"
                :y="
                  safeMapY(speedRange[0]! + ((i - 1) * (speedRange[1]! - speedRange[0]!)) / 5) + 3
                "
                text-anchor="end"
                fill="rgba(255, 255, 255, 0.3)"
                font-size="9"
                font-family="monospace"
              >
                {{ Math.round(speedRange[0]! + ((i - 1) * (speedRange[1]! - speedRange[0]!)) / 5) }}
              </text>
            </g>
            <polygon
              :points="polygonPoints"
              :fill="activeTab === 'CPU' ? 'url(#cpu-glow)' : 'url(#gpu-glow)'"
            />
            <polyline
              :points="polylinePoints"
              fill="none"
              :stroke="activeTab === 'CPU' ? '#8A2BE2' : '#10B981'"
              stroke-width="2.5"
              stroke-linejoin="round"
              stroke-linecap="round"
            />
            <g v-for="(p, index) in currentPoints" :key="index">
              <circle
                :cx="safeMapX(p.temp)"
                :cy="safeMapY(p.speed)"
                r="14"
                fill="transparent"
                cursor="pointer"
                @mousedown.stop="onDragStart(index, $event)"
                @contextmenu.prevent.stop="openContextMenu(index, $event)"
              />
              <circle
                :cx="safeMapX(p.temp)"
                :cy="safeMapY(p.speed)"
                r="5"
                :fill="activeTab === 'CPU' ? '#8A2BE2' : '#10B981'"
                stroke="#fff"
                stroke-width="1.5"
                style="filter: url(#shadow); pointer-events: none"
              />
              <text
                v-if="draggingIndex === index"
                :x="safeMapX(p.temp)"
                :y="safeMapY(p.speed) - 15"
                text-anchor="middle"
                fill="#ffffff"
                font-size="10"
                font-weight="bold"
                font-family="monospace"
                style="pointer-events: none; text-shadow: 0 0 4px rgba(0, 0, 0, 0.8)"
              >
                {{ p.temp }}°C / {{ p.speed }} RPM
              </text>
            </g>
          </svg>

          <div v-else class="loading-state">
            <a-spin :size="20" />
            <span class="ml-2 text-xs text-gray-500">{{
              width === 0 ? '初始化编辑器视图...' : '数据失效'
            }}</span>
          </div>
        </div>
        <div
          v-if="menuVisible"
          class="context-menu"
          :style="menuStyle"
          @click.stop
          @contextmenu.prevent
        >
          <div class="menu-item" @click="onAddNode">在此处右侧添加节点</div>
          <div
            class="menu-item border-t border-white/[0.03]"
            :class="{ disabled: !canDelete }"
            @click="onRemoveNode"
          >
            删除当前节点
          </div>
          <div class="menu-item border-t border-white/[0.03]" @click="openEditModal">
            编辑精确数值
          </div>
        </div>
        <a-modal
          v-model:visible="showEdit"
          title="编辑转速节点"
          :mask-closable="false"
          @ok="onEditConfirm"
        >
          <a-space
            v-if="selectedIndex !== null"
            direction="vertical"
            size="large"
            style="width: 100%"
          >
            <a-input-number
              v-model="editForm.temp"
              :min="getMinTemp(selectedIndex)"
              :max="getMaxTemp(selectedIndex)"
              style="width: 100%"
            >
              <template #prepend>温度 (°C)</template>
            </a-input-number>
            <a-input-number
              v-model="editForm.speed"
              :min="speedRange[0]"
              :max="speedRange[1]"
              style="width: 100%"
            >
              <template #prepend>转速 (RPM)</template>
            </a-input-number>
          </a-space>
        </a-modal>
      </a-card>
      <FanSpeed></FanSpeed>
    </div>
  </div>
</template>

<script lang="ts" setup>
import FanSpeed from '@/components/common/FanSpeed.vue'
import { useFanCurveEditor } from '@/composables/useFanCurveEditor'

const {
  activeTab,
  currentPoints,
  currentTempRange,
  speedRange,
  padding,
  containerRef,
  width,
  height,
  draggingIndex,
  menuVisible,
  selectedIndex,
  showEdit,
  editForm,
  isServiceRunning,
  serviceLoading,
  canDelete,
  isValidRender,
  onTabChange,
  handleServiceToggle,
  handleRemoveFanClick,
  safeMapX,
  safeMapY,
  polylinePoints,
  polygonPoints,
  onDragStart,
  onSvgMouseMove,
  onDragEnd,
  menuStyle,
  openContextMenu,
  closeMenu,
  getMinTemp,
  getMaxTemp,
  onAddNode,
  onRemoveNode,
  openEditModal,
  onEditConfirm,
} = useFanCurveEditor()
</script>

<style lang="scss" scoped>
.no-scrollbar::-webkit-scrollbar {
  display: none;
}
.no-scrollbar {
  -ms-overflow-style: none;
  scrollbar-width: none;
}

.fan-curve-card {
  height: 400px;
  position: relative;
  user-select: none;
  background: rgba(18, 19, 32, 0.6) !important;
  backdrop-filter: blur(12px);
  border: 1px solid rgba(255, 255, 255, 0.05) !important;
  border-radius: 12px;
  box-shadow: 0 8px 32px rgba(0, 0, 0, 0.3);

  :deep(.arco-card-body) {
    height: 100%;
    padding: 0;
    display: flex;
    flex-direction: column;
  }
}

.header-info {
  padding: 12px 20px;
  border-bottom: 1px solid rgba(255, 255, 255, 0.05);
  display: flex;
  justify-content: space-between;
  align-items: center;
  font-size: 12px;
  color: rgba(255, 255, 255, 0.85);
  height: 54px;

  .info-section {
    flex: 1.5;
    display: flex;
    align-items: center;
  }

  .control-section {
    flex: 1;
    display: flex;
    justify-content: center;
    align-items: center;

    .status-tag {
      display: flex;
      align-items: center;
      background-color: rgba(255, 255, 255, 0.02) !important;
      border: 1px solid rgba(255, 255, 255, 0.05) !important;
      color: rgba(255, 255, 255, 0.6);
      height: 24px;
      padding: 0 8px;
    }

    .status-dot {
      width: 6px;
      height: 6px;
      border-radius: 50%;
      background-color: #86909c;
      margin-right: 6px;
      transition: all 0.3s;

      &.active {
        background-color: #00b42a;
        box-shadow: 0 0 6px #00b42a;
        animation: pulse 2s infinite;
      }
    }
  }

  .help-section {
    flex: 1;
    text-align: right;
  }

  .sub-info {
    color: rgba(255, 255, 255, 0.4);
  }
}

@keyframes pulse {
  0% {
    box-shadow: 0 0 0 0 rgba(0, 180, 42, 0.4);
  }
  70% {
    box-shadow: 0 0 0 4px rgba(0, 180, 42, 0);
  }
  100% {
    box-shadow: 0 0 0 0 rgba(0, 180, 42, 0);
  }
}

.svg-container {
  flex: 1;
  width: 100%;
  position: relative;
  overflow: hidden;
  background: transparent;

  .loading-state {
    width: 100%;
    height: 100%;
    display: flex;
    align-items: center;
    justify-content: center;
    color: rgba(255, 255, 255, 0.4);
    font-size: 13px;
  }
}

.context-menu {
  position: absolute;
  z-index: 999;
  background: rgba(26, 27, 43, 0.95);
  backdrop-filter: blur(12px);
  border-radius: 6px;
  box-shadow: 0 8px 24px rgba(0, 0, 0, 0.5);
  border: 1px solid rgba(255, 255, 255, 0.08);
  min-width: 140px;
  padding: 4px 0;

  .menu-item {
    padding: 8px 16px;
    cursor: pointer;
    font-size: 12px;
    color: rgba(255, 255, 255, 0.8);
    transition: all 0.2s;

    &:hover {
      background: rgba(138, 43, 226, 0.15);
      color: #a855f7;
    }

    &.disabled {
      color: rgba(255, 255, 255, 0.25);
      cursor: not-allowed;
      background: transparent !important;
    }
  }
}
:deep(.radio-group-dark.arco-radio-group-button) {
  background-color: rgba(255, 255, 255, 0.03) !important;
  border: 1px solid rgba(255, 255, 255, 0.05) !important;
  border-radius: 8px !important;
  padding: 2px !important;

  .arco-radio-button {
    background-color: transparent !important;
    border: none !important;
    color: rgba(255, 255, 255, 0.5) !important;
    border-radius: 6px !important;
    font-weight: 500 !important;
    font-size: 11px !important;
    transition: all 0.3s !important;
    padding: 0 10px !important;
    height: 24px !important;
    line-height: 24px !important;

    &:not(.arco-radio-button-checked):hover {
      background-color: rgba(255, 255, 255, 0.05) !important;
      color: rgba(255, 255, 255, 0.8) !important;
    }

    &.arco-radio-button-checked {
      background-color: var(--color-accent-purple) !important;
      color: #ffffff !important;
      box-shadow: 0 0 10px rgba(138, 43, 226, 0.3) !important;
    }
  }
}

:deep(.radio-gpu.arco-radio-group-button) {
  .arco-radio-button.arco-radio-button-checked {
    background-color: #10b981 !important;
    box-shadow: 0 0 10px rgba(16, 185, 129, 0.3) !important;
  }
}
:deep(.switch-purple.arco-switch-checked) {
  background-color: var(--color-accent-purple) !important;
}

:deep(.arco-modal) {
  background-color: #121320 !important;
  border: 1px solid rgba(255, 255, 255, 0.08) !important;
  border-radius: 12px !important;
  box-shadow: 0 12px 36px rgba(0, 0, 0, 0.6) !important;

  .arco-modal-header {
    border-bottom: 1px solid rgba(255, 255, 255, 0.05) !important;
    .arco-modal-title {
      color: #ffffff !important;
      font-size: 13px !important;
    }
  }

  .arco-input-number {
    background-color: #17192a !important;
    border: 1px solid rgba(255, 255, 255, 0.05) !important;
    color: #ffffff !important;
    border-radius: 8px !important;
    overflow: hidden;

    .arco-input-number-prepend {
      background-color: #121320 !important;
      border-right: 1px solid rgba(255, 255, 255, 0.05) !important;
      color: rgba(255, 255, 255, 0.5) !important;
      font-size: 11px !important;
    }
  }

  .arco-modal-footer {
    border-t: 1px solid rgba(255, 255, 255, 0.05) !important;
  }
}
</style>
