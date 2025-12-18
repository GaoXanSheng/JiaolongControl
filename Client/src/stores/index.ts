import { defineStore } from 'pinia'
import { HomeTab } from '@/stores/HomeTab.ts'
import HOME from '@/assets/HOME.png'
import CPU from '@/assets/CPU.png'
import Fan from '@/assets/Fan.png'
import Keyboard from '@/assets/Keyboard.png'
import Settings from '@/assets/Settings.png'

const useStore = defineStore('store', {
	state: () => {
		return {
			Debug: false,
			SwitchPages: HomeTab.HOME,
			FanSpeed: 1500,
			CPUData: {
				shortPower: 65,
				longPower: 45,
				tempWall: 88
			},
			theme: 'light',
			ServiceOption: false,
			customVideo: ''
		}
	}
})
export const HomeCardType = [
	{
		title: '主页',
		icon: HOME,
		eum: HomeTab.HOME
	},
	{
		title: '中央处理器',
		icon: CPU,
		eum: HomeTab.CPU
	},
	{
		title: '风扇',
		icon: Fan,
		eum: HomeTab.Fan
	},
	{
		title: '键盘',
		icon: Keyboard,
		eum: HomeTab.Keyboard
	},
	{
		title: '设置',
		icon: Settings,
		eum: HomeTab.Settings
	}
]

export default useStore
