/**
 * 基础传感器数据单元
 * 表示单个监控项的名称和数值
 */
export interface SensorMetric {
	/** 监控项名称 (例如: "Core #1", "GPU Core", "Package") */
	Name: string;
	/** 监控数值 (可能是温度、电压、负载百分比等) */
	Value: number;
}

/**
 * CPU 硬件监控数据
 * 对应 JSON 中的 "Cpu"
 */
export interface CpuMonitorData {
	/** 负载信息 (百分比) */
	Load: SensorMetric[];
	/** 功耗信息 (瓦特 W) */
	Power: SensorMetric[];
	/** 时钟频率信息 (兆赫兹 MHz) */
	Clock: SensorMetric[];
	/** 倍频因子 */
	Factor: SensorMetric[];
	/** 电压信息 (伏特 V) */
	Voltage: SensorMetric[];
	/** 温度信息 (摄氏度 °Ce) */
	Temperature: SensorMetric[];
	/** 硬件名称 (例如: "AMD Ryzen 9 7945HX...") */
	Name: string;
}

/**
 * AMD 核显/显卡监控数据
 * 对应 JSON 中的 "GpuAmd"
 */
export interface GpuAmdMonitorData {
	/** 显存及其他小型数据统计 */
	SmallData: SensorMetric[];
	/** 性能因子 (如 FPS) */
	Factor: SensorMetric[];
	/** 负载信息 (百分比) */
	Load: SensorMetric[];
	/** 时钟频率信息 (MHz) */
	Clock: SensorMetric[];
	/** 温度信息 (°C) */
	Temperature: SensorMetric[];
	/** 电压信息 (V) */
	Voltage: SensorMetric[];
	/** 功耗信息 (W) */
	Power: SensorMetric[];
	/** 硬件名称 */
	Name: string;
}

/**
 * Nvidia 独显监控数据
 * 对应 JSON 中的 "GpuNvidia"
 * 注意：Nvidia 数据结构在此 JSON 中包含 Throughput 且没有 Factor/Voltage 字段
 */
export interface GpuNvidiaMonitorData {
	/** 温度信息 (°C) */
	Temperature: SensorMetric[];
	/** 时钟频率信息 (MHz) */
	Clock: SensorMetric[];
	/** 负载信息 (百分比) */
	Load: SensorMetric[];
	/** 显存及其他小型数据统计 */
	SmallData: SensorMetric[];
	/** 功耗信息 (W) */
	Power: SensorMetric[];
	/** 数据吞吐量 (PCIe 带宽等) */
	Throughput: SensorMetric[];
	/** 硬件名称 */
	Name: string;
}

/**
 * 根接口：硬件监控信息总览
 */
export interface HardwareMonitorInfo {
	/** CPU 数据 */
	Cpu: CpuMonitorData;
	/** AMD GPU 数据 (通常为核显) */
	GpuAmd: GpuAmdMonitorData;
	/** Nvidia GPU 数据 (通常为独显) */
	GpuNvidia: GpuNvidiaMonitorData;
}