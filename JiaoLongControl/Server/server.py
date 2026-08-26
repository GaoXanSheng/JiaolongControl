# -*- coding: utf-8 -*-
"""HTTP Server: 托管预构建前端 WebRoot 并分发 /api/<group>/<method> 到 bridge 分发表."""
import json
import os

from .autofan import apply_boot_config
from .bridge import H

DIST = os.path.join(os.path.dirname(os.path.abspath(__file__)),
                    "..", "..", "bin", "publish", "WebRoot")
MIME = {".html": "text/html", ".js": "text/javascript", ".css": "text/css",
        ".png": "image/png", ".svg": "image/svg+xml", ".ico": "image/x-icon",
        ".woff2": "font/woff2", ".json": "application/json"}

def api_dispatch(group, method, args):
    fn = H.get((group, method))
    if fn is None:
        return {"Success": False, "Message": f"未实现: {group}.{method}"}
    try:
        return fn(args)
    except Exception as e:
        return {"Success": False, "Message": f"{type(e).__name__}: {e}"}

def serve(port=8800):
    from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

    class Handler(BaseHTTPRequestHandler):
        protocol_version = "HTTP/1.1"

        def log_message(self, *a): pass

        def _send(self, body, ctype="application/json"):
            data = body if isinstance(body, bytes) else json.dumps(body, ensure_ascii=False).encode()
            self.send_response(200)
            self.send_header("Content-Type", f"{ctype}; charset=utf-8")
            self.send_header("Content-Length", str(len(data)))
            if "html" in ctype:
                # index.html 不缓存: 保证浏览器总是加载最新 hash 的 assets
                self.send_header("Cache-Control", "no-cache, no-store, must-revalidate")
            self.end_headers()
            self.wfile.write(data)

        def do_GET(self):
            path = self.path.split("?")[0]
            if path.startswith("/api/"):
                parts = path.rstrip("/").partition("/api/")[2].strip("/").split("/")
                res = api_dispatch(parts[0], parts[1] if len(parts) > 1 else "", [])
                return self._send(res)
            if path in ("/", "/index.html"):
                fp = os.path.join(DIST, "index.html")
                if os.path.exists(fp):
                    return self._send(open(fp, "rb").read(), "text/html")
                return self._send("<h1>JiaolongControl Linux</h1><p>前端未构建: cd Client && npm run build</p>", "text/html")
            safe = os.path.normpath(path).lstrip("/")
            fp = os.path.join(DIST, safe)
            if fp.startswith(DIST) and os.path.isfile(fp):
                ext = os.path.splitext(fp)[1]
                return self._send(open(fp, "rb").read(), MIME.get(ext, "application/octet-stream"))
            fp2 = os.path.join(DIST, "index.html")
            if os.path.exists(fp2):
                return self._send(open(fp2, "rb").read(), "text/html")
            self._send({"error": "not found"})

        def do_POST(self):
            path = self.path.split("?")[0]
            if not path.startswith("/api/"):
                return self._send({"error": "not found"})
            ln = int(self.headers.get("Content-Length", 0) or 0)
            try:
                body = json.loads(self.rfile.read(ln) or b"{}")
            except Exception:
                body = {}
            parts = path.rstrip("/").partition("/api/")[2].strip("/").split("/")
            res = api_dispatch(parts[0], parts[1] if len(parts) > 1 else "", body.get("args", []))
            self._send(res)

    try:
        srv = ThreadingHTTPServer(("127.0.0.1", port), Handler)
    except OSError as e:
        if e.errno == 98:
            print(f"错误: 端口 {port} 已被占用。\n"
                  f"  可能已有一个 jlctl serve 在运行:  pgrep -af jlctl.py\n"
                  f"  停止它:                            sudo fuser -k {port}/tcp", flush=True)
            return 1
        raise
    try:
        boot = apply_boot_config()
        print(f"[jlctl] boot config applied: {boot}", flush=True)
    except Exception as e:
        print(f"[jlctl] boot config apply failed: {e}", flush=True)
    print(f"JiaolongControl WebUI: http://127.0.0.1:{port}/  (dist={DIST})", flush=True)
    srv.serve_forever()
