import { createRouter, createWebHashHistory } from 'vue-router'

import Main from '@/pages/Main.vue'
import OpenProSettings from '@/pages/OpenProSettings.vue'

const routes = [
	{ path: '/', component: Main },
	{ path: '/OpenProSettings', component: OpenProSettings }
]

const router = createRouter({
	history: createWebHashHistory(),
	routes
})

export default router
