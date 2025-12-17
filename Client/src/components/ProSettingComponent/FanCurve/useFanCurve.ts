import { reactive, toRaw } from 'vue'
import WMIOperation from '@/utils/WMIOperation.ts'
import { Message } from '@arco-design/web-vue'

export interface Point {
	temp: number
	speed: number
}

export function useFanCurve() {
	const defaultPoints: Point[] = [
		{ temp: 60, speed: 1500 },
		{ temp: 80, speed: 3000 },
		{ temp: 100, speed: 5800 }
	]

	const raw = localStorage.getItem('fanCurve')
	const initial = raw ? (JSON.parse(raw) as Point[]) : defaultPoints
	const points = reactive<Point[]>(initial)

	let lastSerialized = JSON.stringify(points.map((p) => ({ ...p })))
	setInterval(() => {
		const current = JSON.stringify(points.map((p) => ({ ...p })))
		if (current !== lastSerialized) {
			WMIOperation.Fan.SetFanCurve(toRaw(points)).then((res) => {
				Message.success(res)
			})
			localStorage.setItem('fanCurve', current)
			lastSerialized = current
		}
	}, 1000)

	return points
}
