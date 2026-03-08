// <copyright file="AsyncVoidErrorHandler.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic;

using System.Diagnostics;

/// <summary>
/// Provides error handling for async void methods that cannot access a logger.
/// Logs to trace output so errors are visible in production.
/// </summary>
internal static class AsyncVoidErrorHandler
{
    /// <summary>
    /// Handles an exception from an async void method by writing to trace output.
    /// </summary>
    /// <param name="ex">The exception.</param>
    /// <param name="context">A description of where the error occurred.</param>
    public static void HandleException(Exception ex, string context)
    {
        Debug.Fail(ex.Message, ex.StackTrace);
        Trace.TraceError("[{0}] Unhandled exception in async void: {1}", context, ex);
    }
}
