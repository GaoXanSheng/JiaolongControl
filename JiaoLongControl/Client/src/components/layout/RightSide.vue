<script setup lang="ts">
import useStore, { HomeCardType } from '@/stores'
import { computed } from 'vue'

const store = useStore()
const currentComponent = computed(() => {
  for (let i of HomeCardType) {
    if (i.num === store.$state.SwitchPages) {
      return i.page
    }
  }
  return HomeCardType[0]!.page
})

function enter(el: Element, done: () => void) {
  el.classList.add('swap')
  el.addEventListener('animationend', () => {
    done()
  })
}

function leave(_el: Element, done: () => void) {
  done()
}
</script>

<template>
  <div class="rightSide relative">
    <transition mode="out-in" @enter="enter" @leave="leave">
      <Suspense>
        <template #default>
          <component :is="currentComponent" :key="store.$state.SwitchPages" />
        </template>
        <template #fallback>
          <div class="absolute inset-0 flex items-center justify-center bg-[#0D0E15]">
            <div class="flex flex-col items-center gap-3">
              <svg
                class="animate-spin h-8 w-8 text-purple-500"
                xmlns="http://www.w3.org/2000/svg"
                fill="none"
                viewBox="0 0 24 24"
              >
                <circle
                  class="opacity-25"
                  cx="12"
                  cy="12"
                  r="10"
                  stroke="currentColor"
                  stroke-width="4"
                ></circle>
                <path
                  class="opacity-75"
                  fill="currentColor"
                  d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"
                ></path>
              </svg>
              <span class="text-sm text-gray-400">Loading Configuration...</span>
            </div>
          </div>
        </template>
      </Suspense>
    </transition>
  </div>
</template>

<style scoped>
.rightSide {
  width: 100%;
  height: 100%;
}
</style>
