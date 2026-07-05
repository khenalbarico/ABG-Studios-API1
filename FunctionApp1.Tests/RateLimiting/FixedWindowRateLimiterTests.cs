using FunctionApp1.RateLimiting;
using Microsoft.Extensions.Time.Testing;

namespace FunctionApp1.Tests.RateLimiting;

public class FixedWindowRateLimiterTests
{
    [Fact]
    public void Allows_up_to_limit_then_blocks()
    {
        var limiter = new FixedWindowRateLimiter(new FakeTimeProvider());

        Assert.True(limiter.Allow("bookings", "user1", 2, TimeSpan.FromMinutes(1)));
        Assert.True(limiter.Allow("bookings", "user1", 2, TimeSpan.FromMinutes(1)));
        Assert.False(limiter.Allow("bookings", "user1", 2, TimeSpan.FromMinutes(1)));
    }

    [Fact]
    public void Window_reset_allows_again()
    {
        var time    = new FakeTimeProvider();
        var limiter = new FixedWindowRateLimiter(time);

        Assert.True(limiter.Allow("bookings", "user1", 1, TimeSpan.FromMinutes(1)));
        Assert.False(limiter.Allow("bookings", "user1", 1, TimeSpan.FromMinutes(1)));

        time.Advance(TimeSpan.FromMinutes(1));

        Assert.True(limiter.Allow("bookings", "user1", 1, TimeSpan.FromMinutes(1)));
    }

    [Fact]
    public void Keys_and_buckets_are_isolated()
    {
        var limiter = new FixedWindowRateLimiter(new FakeTimeProvider());

        Assert.True(limiter.Allow("bookings", "user1", 1, TimeSpan.FromMinutes(1)));
        Assert.True(limiter.Allow("bookings", "user2", 1, TimeSpan.FromMinutes(1)));
        Assert.True(limiter.Allow("status", "user1", 1, TimeSpan.FromMinutes(1)));
        Assert.False(limiter.Allow("bookings", "user1", 1, TimeSpan.FromMinutes(1)));
    }
}
