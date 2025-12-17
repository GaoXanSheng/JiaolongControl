using System.Text.Json;
using System.Text.Json.Nodes;
using JiaoLongControl.Server.Core.Controllers;
using JiaoLongControl.Server.Core.Services;

namespace JiaoLongControl.Server.Interop
{
    // COM 可见性设置，用于 WebView2 调用
    [System.Runtime.InteropServices.ComVisible(true)]
    public class Bridge
    {
        private CliProgramEnumerationType _cliExecutor = new CliProgramEnumerationType();

        public string Call(string jsonMessage)
        {
            try
            {
                var node = JsonNode.Parse(jsonMessage);
                string channel = node["channel"]?.ToString();
                if (node["args"] is JsonArray argsArr && argsArr.Count >= 2)
                {
                    string typeName = argsArr[0].ToString();
                    string methodName = argsArr[1].ToString();
                    
                    // 解析剩余参数
                    var paramList = new System.Collections.Generic.List<string>();
                    for(int i = 2; i < argsArr.Count; i++) paramList.Add(argsArr[i].ToString());

                    return _cliExecutor.EumType(typeName, methodName, paramList.ToArray());
                }
                
                // 处理配置保存
                if (channel == "SetFanCurve")
                {
                    string data = node["data"]?.ToString();
                    FanControlService.ReloadFanCurve(data);
                    System.IO.File.WriteAllText(System.IO.Path.Combine(AppContext.BaseDirectory, "fanCurve.json"), data);
                    return JsonSerializer.Serialize(new { result = "Saved" });
                }

                return JsonSerializer.Serialize(new { result = false, msg = "Unknown Channel" });
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { result = false, msg = ex.Message });
            }
        }
    }
}