# -*- coding: utf-8 -*-
"""CommandResult 助手: 所有 API/CLI 输出统一为 {Success, Message, Data?}."""

def ok(msg="获取成功", data=None):
    return {"Success": True, "Message": msg, **({"Data": data} if data is not None else {})}

def fail(msg="设置失败", data=None):
    return {"Success": False, "Message": msg, **({"Data": data} if data is not None else {})}
