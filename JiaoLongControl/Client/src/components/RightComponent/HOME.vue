<script setup lang="ts">
import BackgroundVideo from '@/assets/BackgroundVideo.mp4'
</script>

<template>
  <a-layout class="layout-Content">
    <a-layout>
      <a-layout-header style="padding-left: 20px;">
      </a-layout-header>
      <a-layout style="padding: 0 30px;">
        <a-layout-content>
          <video :src="BackgroundVideo" muted loop autoplay></video>
          <a-typography>
            <a-typography-title :heading="2"></a-typography-title>
            <a-typography-paragraph blockquote>
              <a-typography-text>
                Thaumaturgy... which giveth certain order to make strange works, of the sense to be
                perceived and of men greatly to be wondered at.MathematicallPraeface.
              </a-typography-text>
              <br/>
              <a-typography-text bold> John Dee(1570) ——《Euclid's Elements》</a-typography-text>
            </a-typography-paragraph>
          </a-typography>
        </a-layout-content>
      </a-layout>
    </a-layout>
  </a-layout>
</template>

<style lang="scss" scoped>
video {
  width: 600px;
  height: 350px;
  margin: 0 auto;
  padding: 0;
  object-fit: cover;
  border-radius: 5px;
  box-shadow: 0 0 2px rgba(0, 0, 0, 0.6);
  overflow: hidden;
  display: block;
}
</style>
