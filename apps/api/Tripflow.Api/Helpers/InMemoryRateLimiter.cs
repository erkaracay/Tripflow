using System.Collections.Concurrent;

namespace Tripflow.Api.Helpers;

/// <summary>
/// Process-local sliding-window rate limiter.
///
/// Bounded by a soft cap on bucket count (<see cref="MaxBuckets"/>) and a
/// periodic sweep (<see cref="SweepInterval"/>) that drops stale timestamps
/// and empty buckets. Thread-safe via per-bucket locks.
///
/// Not suitable for multi-instance deployments on its own — each process
/// maintains its own counters. If horizontal scaling is introduced, swap
/// the storage for Redis or equivalent.
/// </summary>
internal sealed class InMemoryRateLimiter
{
    public static InMemoryRateLimiter Default { get; } = new();

    private const int MaxBuckets = 50_000;
    private static readonly TimeSpan SweepInterval = TimeSpan.FromMinutes(5);
    // Longest window we actually track (verify-ip). Used by the sweeper to
    // drop timestamps that could not possibly matter for any caller.
    private static readonly TimeSpan MaxTrackedWindow = TimeSpan.FromHours(1);

    private readonly ConcurrentDictionary<string, Bucket> _buckets = new();
    private long _lastSweepTicks;

    private sealed class Bucket
    {
        public readonly List<DateTime> Hits = new();
    }

    /// <summary>
    /// Returns <c>(true, retryAfterSeconds)</c> when the bucket for <paramref name="key"/>
    /// already holds at least <paramref name="maxCount"/> hits within the rolling
    /// <paramref name="window"/>. Does NOT record a hit — call <see cref="RegisterHit"/>
    /// after a confirmed failure.
    /// </summary>
    public (bool Limited, int RetryAfterSeconds) CheckLimit(string key, int maxCount, TimeSpan window)
    {
        MaybeSweep();
        var bucket = _buckets.GetOrAdd(key, static _ => new Bucket());
        lock (bucket.Hits)
        {
            var now = DateTime.UtcNow;
            bucket.Hits.RemoveAll(t => now - t > window);
            if (bucket.Hits.Count < maxCount)
                return (false, 0);

            var oldest = bucket.Hits[0];
            var retry = (int)Math.Ceiling((window - (now - oldest)).TotalSeconds);
            return (true, Math.Max(retry, 1));
        }
    }

    /// <summary>
    /// Records a hit at "now" for <paramref name="key"/>, pruning stale entries first.
    /// </summary>
    public void RegisterHit(string key, TimeSpan window)
    {
        var bucket = _buckets.GetOrAdd(key, static _ => new Bucket());
        lock (bucket.Hits)
        {
            var now = DateTime.UtcNow;
            bucket.Hits.RemoveAll(t => now - t > window);
            bucket.Hits.Add(now);
        }
    }

    /// <summary>
    /// Clears the bucket for <paramref name="key"/>. Used after a successful
    /// login to drop a failure counter.
    /// </summary>
    public void Clear(string key)
    {
        if (_buckets.TryRemove(key, out _))
        {
            // Bucket dropped entirely — no need to hold the lock or empty the list.
        }
    }

    private void MaybeSweep()
    {
        var last = Interlocked.Read(ref _lastSweepTicks);
        var nowTicks = DateTime.UtcNow.Ticks;
        var sinceLast = TimeSpan.FromTicks(nowTicks - last);

        if (sinceLast < SweepInterval && _buckets.Count < MaxBuckets)
            return;

        // One sweeper at a time; losers skip this pass.
        if (Interlocked.CompareExchange(ref _lastSweepTicks, nowTicks, last) != last)
            return;

        Sweep();
    }

    private void Sweep()
    {
        var now = DateTime.UtcNow;
        foreach (var kv in _buckets)
        {
            lock (kv.Value.Hits)
            {
                kv.Value.Hits.RemoveAll(t => now - t > MaxTrackedWindow);
                if (kv.Value.Hits.Count == 0)
                    _buckets.TryRemove(kv.Key, out _);
            }
        }
    }
}
