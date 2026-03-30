using System.Runtime.InteropServices;
using System.Text.Encodings.Web;
using System.Text.Json;
using JiaoLongControl.Server.Core.Controllers;
using log4net;

namespace JiaoLongControl.Server.Core.Utils;

[ComVisible(true)]
[ClassInterface(ClassInterfaceType.AutoDual)]
public class CommandResult
{
    private readonly ILog Logger= LogManager.GetLogger(typeof(AutoFanControl));
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false
    };

    public bool Success { get; set; }
    public string Message { get; set; }
    public object Data { get; set; }

    public CommandResult(bool success, string message, object data = null)
    {
        Success = success;
        Message = message;
        Data = data;
        Logger.Info($"{success} {message} {data}");
    }

    public string toJson() => JsonSerializer.Serialize(this, JsonOptions);
}