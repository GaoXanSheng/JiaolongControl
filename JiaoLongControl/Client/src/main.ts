import {createApp} from 'vue'
import App from './App.vue'
import './style.css'
// 函数式 API (Message) 样式: 组件样式由 unplugin-vue-components 按需注入
import '@arco-design/web-vue/es/message/style/css.js'
import {createPinia} from 'pinia'
import {useConfigStore} from '@/stores/config'
import router from '@/router/routes.ts'
import './assets/Global.scss'
import {timing} from '@/utils/timing'

const t0 = timing('script start')
const pinia = createPinia()
const app = createApp(App).use(pinia).use(router)

useConfigStore(pinia)
  .fetchConfig()
  .finally(() => {
    timing('config loaded', t0)
    app.mount('#app')
    timing('app mounted', t0)
  })
