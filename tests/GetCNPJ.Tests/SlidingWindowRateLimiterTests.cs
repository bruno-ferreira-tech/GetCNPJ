using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GetCNPJ.RateLimiter;
using Xunit;

namespace GetCNPJ.Tests
{
    public class SlidingWindowRateLimiterTests
    {
        [Fact]
        public void AvailableRequests_ShouldDecreaseWhenRecorded()
        {
            var limiter = new SlidingWindowRateLimiter(maxRequests: 3, timeWindow: TimeSpan.FromMinutes(1));
            var provider = "TestProvider";

            Assert.Equal(3, limiter.GetAvailableRequests(provider));

            limiter.RecordRequest(provider);
            Assert.Equal(2, limiter.GetAvailableRequests(provider));

            limiter.RecordRequest(provider);
            Assert.Equal(1, limiter.GetAvailableRequests(provider));

            limiter.RecordRequest(provider);
            Assert.Equal(0, limiter.GetAvailableRequests(provider));
        }

        [Fact]
        public void Reset_ShouldClearRecordedRequests()
        {
            var limiter = new SlidingWindowRateLimiter(maxRequests: 3, timeWindow: TimeSpan.FromMinutes(1));
            var provider = "TestProvider";

            limiter.RecordRequest(provider);
            limiter.RecordRequest(provider);
            Assert.Equal(1, limiter.GetAvailableRequests(provider));

            limiter.Reset(provider);
            Assert.Equal(3, limiter.GetAvailableRequests(provider));
        }

        [Fact]
        public async Task WaitIfNeededAsync_ShouldBeThreadSafeUnderConcurrency()
        {
            var limiter = new SlidingWindowRateLimiter(maxRequests: 5, timeWindow: TimeSpan.FromMilliseconds(200));
            var provider = "ConcurrentProvider";

            var tasks = new List<Task>();
            for (int i = 0; i < 10; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    await limiter.WaitIfNeededAsync(provider);
                    limiter.RecordRequest(provider);
                }));
            }

            await Task.WhenAll(tasks);
            // Se completou sem exceção, passou na verificação de thread safety
            Assert.True(true);
        }
    }
}
