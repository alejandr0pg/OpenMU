namespace MUnique.OpenMU.Web.API;

using System.Collections.Concurrent;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Handles OAuth callback from providers and serves pending codes to game clients.
/// </summary>
[Route("auth/")]
public class OAuthCallbackController : Controller
{
    private static readonly ConcurrentDictionary<string, PendingAuth> PendingCodes = new();

    /// <summary>
    /// OAuth callback endpoint. Receives authorization code from provider.
    /// </summary>
    [HttpGet("callback")]
    public IActionResult Callback(
        [FromQuery] string code,
        [FromQuery] string state,
        [FromQuery] string? error)
    {
        if (!string.IsNullOrEmpty(error))
        {
            return this.Content(BuildHtmlPage("Login Failed",
                "Authentication was cancelled or failed. You can close this window."), "text/html");
        }

        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
        {
            return this.BadRequest("Missing code or state parameter.");
        }

        CleanExpiredEntries();

        PendingCodes[state] = new PendingAuth(code, DateTime.UtcNow.AddMinutes(2));

        return this.Content(BuildHtmlPage("Login Successful",
            "You can close this window and return to the game."), "text/html");
    }

    /// <summary>
    /// Polling endpoint for game client to retrieve the authorization code.
    /// </summary>
    [HttpGet("poll")]
    public IActionResult Poll([FromQuery] string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return this.BadRequest("Missing id parameter.");
        }

        if (PendingCodes.TryRemove(id, out var pending))
        {
            if (DateTime.UtcNow > pending.Expiry)
            {
                return this.Json(new { status = "expired" });
            }

            return this.Json(new { status = "ok", code = pending.Code });
        }

        return this.Json(new { status = "pending" });
    }

    private static void CleanExpiredEntries()
    {
        var expired = PendingCodes
            .Where(kv => DateTime.UtcNow > kv.Value.Expiry)
            .Select(kv => kv.Key)
            .ToList();

        foreach (var key in expired)
        {
            PendingCodes.TryRemove(key, out _);
        }
    }

    private static string BuildHtmlPage(string title, string message)
    {
        return $"""
            <!DOCTYPE html>
            <html>
            <head><title>{title}</title>
            <style>
                body {{ font-family: sans-serif; display: flex; justify-content: center;
                       align-items: center; min-height: 100vh; margin: 0;
                       background: #1a1a2e; color: #e0e0e0; }}
                .card {{ text-align: center; padding: 2rem; border-radius: 12px;
                         background: #16213e; border: 1px solid #0f3460; }}
                h1 {{ color: #e94560; margin-bottom: 0.5rem; }}
            </style></head>
            <body><div class="card"><h1>{title}</h1><p>{message}</p></div></body>
            </html>
            """;
    }

    private sealed record PendingAuth(string Code, DateTime Expiry);
}
