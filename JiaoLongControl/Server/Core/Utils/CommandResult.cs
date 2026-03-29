namespace JiaoLongControl.Server.Core.Utils;

public class CommandResult
{
    public bool Success { get; set; }
    public string Message { get; set; }

    public CommandResult(bool success, string message)
    {
        Success = success;
        Message = message.Trim();
    }
}