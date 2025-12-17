export function useCoordinateTransform(
	width: number,
	height: number,
	margin: number,
	tempRange: [number, number],
	speedRange: [number, number]
) {
	const tempToX = (t: number) =>
		margin + ((t - tempRange[0]) / (tempRange[1] - tempRange[0])) * (width - 2 * margin)

	const speedToY = (s: number) =>
		height -
		margin -
		((s - speedRange[0]) / (speedRange[1] - speedRange[0])) * (height - 2 * margin)

	const xToTemp = (x: number) =>
		tempRange[0] + ((x - margin) / (width - 2 * margin)) * (tempRange[1] - tempRange[0])

	const yToSpeed = (y: number) =>
		speedRange[0] +
		((height - margin - y) / (height - 2 * margin)) * (speedRange[1] - speedRange[0])

	return { tempToX, speedToY, xToTemp, yToSpeed }
}
