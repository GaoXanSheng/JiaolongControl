<script setup lang="ts">
import useStore, {HomeCardType} from '@/stores'
import {computed} from 'vue'

const store = useStore()
const currentComponent = computed(() => {
  for (let i of HomeCardType) {
    if (i.eum === store.$state.SwitchPages) {
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
  <div class="rightSide">
    <transition class="magictime" @enter="enter" @leave="leave">
      <component :is="currentComponent" :key="store.$state.SwitchPages"/>
    </transition>
  </div>
</template>

<style scoped>
.rightSide {
  width: 100%;
  height: 100%;
}
</style>
