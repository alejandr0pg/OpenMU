// <copyright file="DaprAuthFilter.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Dapr.Common;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

/// <summary>
/// Action filter that rejects requests not originating from a Dapr sidecar.
/// Apply at controller or action level to restrict access to internal service calls.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class DaprAuthFilter : ActionFilterAttribute
{
    /// <inheritdoc />
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (!IsDaprRequest(context.HttpContext))
        {
            context.Result = new StatusCodeResult(403);
            return;
        }

        base.OnActionExecuting(context);
    }

    private static bool IsDaprRequest(Microsoft.AspNetCore.Http.HttpContext httpContext)
    {
        return httpContext.Request.Headers.ContainsKey("dapr-api-token")
            || (httpContext.Request.Headers.TryGetValue("User-Agent", out var agent)
                && agent.ToString().Contains("dapr", StringComparison.OrdinalIgnoreCase));
    }
}
