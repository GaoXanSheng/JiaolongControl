import { createApp } from 'vue'
import App from './App.vue'
import './style.css'
import './assets/magic.min.css'
// 函数式 API (Message) 样式: 组件样式由 unplugin-vue-components 按需注入
import '@arco-design/web-vue/es/message/style/css.js'
import { createPinia } from 'pinia'
import router from '@/router/routes.ts'
import './assets/Global.scss'
createApp(App).use(createPinia()).use(router).mount('#app')
