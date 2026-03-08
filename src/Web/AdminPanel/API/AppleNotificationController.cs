namespace MUnique.OpenMU.Web.API;

using System;
using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.Persistence;

/// <summary>
/// Handles Sign In with Apple server-to-server notifications.
/// Apple sends events when users revoke consent, delete accounts, or change email preferences.
/// </summary>
[Route("auth/")]
public class AppleNotificationController : Controller
{
    private readonly IPersistenceContextProvider _contextProvider;
    private readonly ILogger<AppleNotificationController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AppleNotificationController"/> class.
    /// </summary>
    public AppleNotificationController(
        IPersistenceContextProvider contextProvider,
        ILogger<AppleNotificationController> logger)
    {
        this._contextProvider = contextProvider;
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
            var handler = new JwtSecurityTokenHandler();
            var token = handler.ReadJwtToken(payload);
            var eventsClaim = token.Claims
                .FirstOrDefault(c => c.Type == "events")?.Value;

            if (string.IsNullOrEmpty(eventsClaim))
            {
                this._logger.LogWarning("Apple notification missing events claim.");
                return this.Ok();
            }

            using var doc = JsonDocument.Parse(eventsClaim);
            var root = doc.RootElement;
            var eventType = root.TryGetProperty("type", out var typeProp)
                ? typeProp.GetString()
                : null;

            var sub = token.Subject;

            this._logger.LogInformation(
                "Apple notification: type={EventType}, sub={Subject}",
                eventType, sub);

            await this.HandleEventAsync(eventType, sub).ConfigureAwait(false);

            return this.Ok();
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Failed to process Apple notification.");
            return this.Ok();
        }
    }

    private async Task HandleEventAsync(string? eventType, string? appleUserId)
    {
        if (string.IsNullOrEmpty(appleUserId))
        {
            return;
        }

        switch (eventType)
        {
            case "consent-revoked":
            case "account-delete":
                this._logger.LogWarning(
                    "Apple user {AppleUserId} triggered {EventType}. Manual review may be needed.",
                    appleUserId, eventType);
                break;

            case "email-enabled":
                this._logger.LogInformation(
                    "Apple user {AppleUserId} enabled email forwarding.",
                    appleUserId);
                break;

            case "email-disabled":
                this._logger.LogInformation(
                    "Apple user {AppleUserId} disabled email forwarding.",
                    appleUserId);
                break;

            default:
                this._logger.LogInformation(
                    "Apple notification unknown event: {EventType} for {AppleUserId}.",
                    eventType, appleUserId);
                break;
        }

        await Task.CompletedTask.ConfigureAwait(false);
    }
}
