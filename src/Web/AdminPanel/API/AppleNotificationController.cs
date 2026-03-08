namespace MUnique.OpenMU.Web.API;

using System;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

/// <summary>
/// Handles Sign In with Apple server-to-server notifications.
/// </summary>
[Route("auth/")]
public class AppleNotificationController : Controller
{
    private readonly ILogger<AppleNotificationController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AppleNotificationController"/> class.
    /// </summary>
    public AppleNotificationController(ILogger<AppleNotificationController> logger)
    {
        this._logger = logger;
    }

    /// <summary>
    /// Receives Sign In with Apple server-to-server notifications.
    /// </summary>
    [HttpPost("apple-notifications")]
    [Consumes("application/x-www-form-urlencoded")]
    public async Task<IActionResult> ReceiveNotification([FromForm] string payload)
    {
        if (string.IsNullOrEmpty(payload))
        {
            return this.BadRequest("Missing payload.");
        }

        try
        {
            var claimsNullable = ParseJwtPayload(payload);
            if (claimsNullable == null)
            {
                this._logger.LogWarning("Apple notification: could not parse JWT payload.");
                return this.Ok();
            }

            var claims = claimsNullable.Value;
            var eventType = GetNestedEventType(claims);
            var sub = claims.TryGetProperty("sub", out var subProp) ? subProp.GetString() : null;

            this._logger.LogInformation(
                "Apple notification: type={EventType}, sub={Subject}",
                eventType, sub);

            LogEvent(eventType, sub);

            return this.Ok();
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Failed to process Apple notification.");
            return this.Ok();
        }
    }

    private static JsonElement? ParseJwtPayload(string jwt)
    {
        var parts = jwt.Split('.');
        if (parts.Length < 2)
        {
            return null;
        }

        var padded = parts[1]
            .Replace('-', '+')
            .Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 2: padded += "=="; break;
            case 3: padded += "="; break;
        }

        var bytes = Convert.FromBase64String(padded);
        var json = Encoding.UTF8.GetString(bytes);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    private static string? GetNestedEventType(JsonElement claims)
    {
        if (!claims.TryGetProperty("events", out var eventsProp))
        {
            return null;
        }

        var eventsJson = eventsProp.ValueKind == JsonValueKind.String
            ? eventsProp.GetString()
            : eventsProp.GetRawText();

        if (string.IsNullOrEmpty(eventsJson))
        {
            return null;
        }

        using var doc = JsonDocument.Parse(eventsJson);
        return doc.RootElement.TryGetProperty("type", out var typeProp)
            ? typeProp.GetString()
            : null;
    }

    private void LogEvent(string? eventType, string? appleUserId)
    {
        switch (eventType)
        {
            case "consent-revoked":
            case "account-delete":
                this._logger.LogWarning(
                    "Apple user {AppleUserId} triggered {EventType}.",
                    appleUserId, eventType);
                break;

            default:
                this._logger.LogInformation(
                    "Apple notification: {EventType} for {AppleUserId}.",
                    eventType, appleUserId);
                break;
        }
    }
}
