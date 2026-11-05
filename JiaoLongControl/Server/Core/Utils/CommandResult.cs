using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace JiaoLongControl.Server.Core.Utils;

[ComVisible(true)]
[ClassInterface(ClassInterfaceType.AutoDual)]
public class CommandResult
{
    private static readonly Log Logger = Log.For<CommandResult>();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false
    };

    public CommandResult(bool success, string message, object data = null,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0,
        [CallerMemberName] string member = "")
    {
        Success = success;
        Message = message;
        Data = data;
        Logger.Debug($"{success} {message} {Describe(data)}", file, line, member);
    }

    public bool Success { get; set; }
    public string Message { get; set; }
    public object Data { get; set; }

    public string toJson() => JsonSerializer.Serialize(this, JsonOptions);

    private static string Describe(object data)
    {
        switch (data)
        {
            case null:
                return "";
            case string text:
                return text;
        }

        try
        {
            return JsonSerializer.Serialize(data, JsonOptions);
        }
        catch
        {
            return data.ToString() ?? "";
        }
    }
}
