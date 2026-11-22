namespace SwitchToKVM.Core.Ipc;

public sealed record PipeResponse(bool Ok, string Message)
{
    public static PipeResponse Success(string message) => new(true, message);
    public static PipeResponse Error(string message) => new(false, message);

    public string Serialize() => $"{(Ok ? "ok" : "error")}\n{Message}";

    public static PipeResponse? Parse(string text)
    {
        var newline = text.IndexOf('\n');
        var head = newline < 0 ? text : text[..newline];
        var message = newline < 0 ? "" : text[(newline + 1)..];
        return head switch
        {
            "ok" => Success(message),
            "error" => Error(message),
            _ => null,
        };
    }
}
