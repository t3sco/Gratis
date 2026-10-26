using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Gratis.Rendering;

public enum FeedbackState { Idle, Sending, Sent, Failed }

public static class FeedbackSender {
    public const int MaxLength = 1500;
    private const string Endpoint = "https://gratis-feedback.vercel.app/api/feedback";
    private static readonly TimeSpan Cooldown = TimeSpan.FromSeconds(30);
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };
    private static DateTime lastSent = DateTime.MinValue;
    private static volatile FeedbackState state;
    private static volatile string error = "";

    public static FeedbackState State => state;
    public static string Error => error;
    public static bool CanSend => state != FeedbackState.Sending && DateTime.UtcNow - lastSent >= Cooldown;

    public static Func<string, Task<bool>> Post { get; set; } = PostJson;

    public static void Reset() {
        if (state != FeedbackState.Sending)
            state = FeedbackState.Idle;
    }

    public static void Send(string message, string? report) {
        message = message.Trim();
        if (message.Length == 0 || !CanSend)
            return;
        state = FeedbackState.Sending;
        error = "";
        var version = typeof(FeedbackSender).Assembly.GetName().Version?.ToString() ?? "unknown";
        var json = JsonSerializer.Serialize(new {
            message = message.Length > MaxLength ? message[..MaxLength] : message,
            version,
            report,
        });
        _ = Task.Run(async () => {
            try {
                if (await Post(json).ConfigureAwait(false)) {
                    lastSent = DateTime.UtcNow;
                    state = FeedbackState.Sent;
                }
                else
                    Fail("The feedback server turned it down. Try again later.");
            }
            catch (Exception) {
                Fail("Couldn't reach the feedback server.");
            }
        });
    }

    private static void Fail(string reason) {
        error = reason;
        state = FeedbackState.Failed;
    }

    private static async Task<bool> PostJson(string json) {
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await Http.PostAsync(Endpoint, content).ConfigureAwait(false);
        return response.IsSuccessStatusCode;
    }
}
