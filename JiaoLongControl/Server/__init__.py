# -*- coding: utf-8 -*-
"""JiaolongControl Linux 后端包: 由 linux-port/jlctl.py 薄入口调用.
模块划分: common(结果助手) / ecf2+portec(EC 硬件访问) / hardware(sysfs 助手与单例)
          / mifs+cpu_power+gpu+system_info+autofan+smu(功能控制器)
          / bridge(API 分发表) / server(HTTP) / cli(argparse 子命令)."""
