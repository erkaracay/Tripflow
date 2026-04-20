using System.Collections;
using System.Reflection;
using Tripflow.Api.Helpers;

namespace Tripflow.Api.Tests.Infrastructure;

/// <summary>
/// InMemoryRateLimiter is a process-local singleton, so tests need to clear the
/// shared bucket dictionary between cases. No DI hook exists yet — we reach into
/// the private field via reflection. Replace with proper DI registration if this
/// limiter gets refactored.
/// </summary>
public static class RateLimiterReset
{
    private static readonly FieldInfo BucketsField =
        typeof(InMemoryRateLimiter).GetField("_buckets", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("InMemoryRateLimiter._buckets field not found.");

    public static void Clear()
    {
        var buckets = (IDictionary)BucketsField.GetValue(InMemoryRateLimiter.Default)!;
        buckets.Clear();
    }
}
