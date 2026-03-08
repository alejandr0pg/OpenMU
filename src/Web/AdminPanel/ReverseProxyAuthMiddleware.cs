// <copyright file="ReverseProxyAuthMiddleware.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel;

using System;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

/// <summary>
/// Middleware that enforces Basic Auth for the admin panel.
/// Validates credentials against ADMIN_USER and ADMIN_PASSWORD environment variables.
/// </summary>
public sealed class ReverseProxyAuthMiddleware
{
    private static readonly string[] PublicPrefixes =
        ["/_framework", "/_blazor", "/_content", "/css", "/js", "/favicon", "/auth/", "/api/register", "/api/status", "/api/is-online", "/health"];

    private readonly RequestDelegate _next;
    private readonly ILogger<ReverseProxyAuthMiddleware> _logger;
    private readonly string _adminUser;
    private readonly string _adminPasswordHash;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReverseProxyAuthMiddleware"/> class.
    /// </summary>
    public ReverseProxyAuthMiddleware(RequestDelegate next, ILogger<ReverseProxyAuthMiddleware> logger)
    {
        this._next = next;
        this._logger = logger;
        this._adminUser = Environment.GetEnvironmentVariable("ADMIN_USER") ?? "admin";
        this._adminPasswordHash = Environment.GetEnvironmentVariable("ADMIN_PASSWORD_HASH") ?? string.Empty;

        if (string.IsNullOrEmpty(this._adminPasswordHash))
        {
            this._logger.LogWarning("ADMIN_PASSWORD_HASH not set. Admin panel is NOT secured. Set it via environment variable.");
        }
    }

    /// <summary>
    /// Invokes the middleware.
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        if (string.Equals(path, "/health", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = 200;
            await context.Response.WriteAsync("ok").ConfigureAwait(false);
            return;
        }

        if (IsPublicResource(path))
        {
            await this._next(context).ConfigureAwait(false);
            return;
        }

        if (!this.ValidateCredentials(context))
        {
            this._logger.LogWarning(
                "Rejected unauthenticated request to {Path} from {IP}",
                path, context.Connection.RemoteIpAddress);

            context.Response.StatusCode = 401;
            context.Response.Headers.Append("WWW-Authenticate", "Basic realm=\"OpenMU Admin\"");
            await context.Response.WriteAsync("Authentication required.").ConfigureAwait(false);
            return;
        }

        await this._next(context).ConfigureAwait(false);
    }

    private static bool IsPublicResource(string path)
    {
        return Array.Exists(PublicPrefixes, prefix =>
            path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    private bool ValidateCredentials(HttpContext context)
    {
        if (string.IsNullOrEmpty(this._adminPasswordHash))
        {
            return false;
        }

        var authHeader = context.Request.Headers["Authorization"].ToString();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            var encoded = authHeader["Basic ".Length..].Trim();
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
            var separatorIndex = decoded.IndexOf(':');

            if (separatorIndex < 0)
            {
                return false;
            }

            var user = decoded[..separatorIndex];
            var password = decoded[(separatorIndex + 1)..];

            return string.Equals(user, this._adminUser, StringComparison.Ordinal)
                && BCrypt.Net.BCrypt.Verify(password, this._adminPasswordHash);
        }
        catch (Exception ex)
        {
            this._logger.LogWarning(ex, "Failed to parse Basic Auth header.");
            return false;
        }
    }
}
