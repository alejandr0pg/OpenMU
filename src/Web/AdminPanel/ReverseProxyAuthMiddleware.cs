// <copyright file="ReverseProxyAuthMiddleware.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

/// <summary>
/// Middleware that rejects requests not coming through an authenticated reverse proxy.
/// Acts as defense-in-depth: even if the admin panel port is exposed directly,
/// unauthenticated requests are blocked at the application level.
/// </summary>
public sealed class ReverseProxyAuthMiddleware
{
    private static readonly string[] PublicPrefixes = ["/_framework", "/_blazor", "/_content", "/css", "/js", "/favicon"];

    private readonly RequestDelegate _next;
    private readonly ILogger<ReverseProxyAuthMiddleware> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReverseProxyAuthMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    /// <param name="logger">The logger.</param>
    public ReverseProxyAuthMiddleware(RequestDelegate next, ILogger<ReverseProxyAuthMiddleware> logger)
    {
        this._next = next;
        this._logger = logger;
    }

    /// <summary>
    /// Invokes the middleware.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        if (IsPublicResource(path))
        {
            await this._next(context).ConfigureAwait(false);
            return;
        }

        if (!HasProxyAuthentication(context))
        {
            this._logger.LogWarning("Rejected unauthenticated request to {Path} from {IP}", path, context.Connection.RemoteIpAddress);
            context.Response.StatusCode = 401;
            context.Response.Headers.Append("WWW-Authenticate", "Basic realm=\"OpenMU Admin\"");
            await context.Response.WriteAsync("Authentication required. Access the admin panel through the reverse proxy.").ConfigureAwait(false);
            return;
        }

        await this._next(context).ConfigureAwait(false);
    }

    private static bool IsPublicResource(string path)
    {
        return Array.Exists(PublicPrefixes, prefix => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasProxyAuthentication(HttpContext context)
    {
        return context.Request.Headers.ContainsKey("Authorization")
            || context.Request.Headers.ContainsKey("X-WEBAUTH-USER");
    }
}
