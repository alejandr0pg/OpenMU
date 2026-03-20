// <copyright file="ConnectionAutoFlusher.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Network;

using System.Collections.Concurrent;
using System.Threading;

/// <summary>
/// Periodically flushes connections that have buffered (unflushed) data.
/// Used together with <see cref="ConnectionExtensions.SendWithoutFlushAsync"/>
/// to batch multiple small packets into fewer TCP segments.
/// </summary>
public sealed class ConnectionAutoFlusher : IDisposable
{
    /// <summary>
    /// Flush interval in milliseconds (~120 Hz).
    /// </summary>
    private const int FlushIntervalMs = 8;

    private static readonly Lazy<ConnectionAutoFlusher> LazyInstance = new(() => new ConnectionAutoFlusher());

    private readonly ConcurrentDictionary<IConnection, byte> _dirtyConnections = new();
    private readonly Timer _timer;
    private bool _disposed;

    private ConnectionAutoFlusher()
    {
        _timer = new Timer(FlushCallback, null, FlushIntervalMs, FlushIntervalMs);
    }

    /// <summary>
    /// Gets the singleton instance.
    /// </summary>
    public static ConnectionAutoFlusher Instance => LazyInstance.Value;

    /// <summary>
    /// Marks a connection as having unflushed data.
    /// </summary>
    public void MarkDirty(IConnection connection)
    {
        _dirtyConnections.TryAdd(connection, 0);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer.Dispose();
    }

    private void FlushCallback(object? state)
    {
        _ = FlushAllAsync();
    }

    private async Task FlushAllAsync()
    {
        foreach (var kvp in _dirtyConnections)
        {
            var connection = kvp.Key;
            _dirtyConnections.TryRemove(connection, out _);

            if (!connection.Connected)
                continue;

            try
            {
                using var l = await connection.OutputLock.LockAsync().ConfigureAwait(false);
                await connection.Output.FlushAsync().ConfigureAwait(false);
            }
            catch
            {
                // Connection may have been disposed between check and flush.
            }
        }
    }
}
