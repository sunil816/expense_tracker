using System;

namespace ExpenseTracker.Tests;

/// <summary>Fixed clock for deterministic report-default-date tests; local time == UTC.</summary>
internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;

    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
}
