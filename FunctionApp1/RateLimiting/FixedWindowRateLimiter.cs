using System.Collections.Concurrent;

namespace FunctionApp1.RateLimiting;

public interface IRateLimiter
{
    /// <summary>Returns false when the caller identified by key exceeded the bucket's limit for the current window.</summary>
    bool Allow(string bucket, string key, int limit, TimeSpan window);
}

/// <summary>
/// In-memory fixed-window limiter for cost control and abuse prevention.
/// Per-instance state is acceptable at this traffic profile (CLAUDE.md §6).
/// </summary>
public sealed class FixedWindowRateLimiter(TimeProvider? timeProvider = null) : IRateLimiter
{
    readonly TimeProvider _time = timeProvider ?? TimeProvider.System;
    readonly ConcurrentDictionary<string, Window> _windows = new();

    sealed class Window
    {
        public long WindowStartTicks;
        public int  Count;
    }

    public bool Allow(string bucket, string key, int limit, TimeSpan window)
    {
        var now        = _time.GetUtcNow().UtcTicks;
        var entry      = _windows.GetOrAdd($"{bucket}:{key}", _ => new Window { WindowStartTicks = now });

        lock (entry)
        {
            if (now - entry.WindowStartTicks >= window.Ticks)
            {
                entry.WindowStartTicks = now;
                entry.Count            = 0;
            }

            if (entry.Count >= limit)
                return false;

            entry.Count++;
            return true;
        }
    }
}
