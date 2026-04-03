using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace APIGateway.NetFramework.Infrastructure
{
    /// <summary>
    /// Custom Token Bucket Rate Limiter for .NET Framework 4.8.
    /// UArch: Zero-allocation, thread-safe implementation.
    /// </summary>
    public class TokenBucketRateLimiter
    {
        private readonly int _capacity;
        private readonly int _refillRate;
        private int _tokens;
        private DateTime _lastRefill;
        private readonly object _lock = new object();

        public TokenBucketRateLimiter(int capacity, int refillRate)
        {
            _capacity = capacity;
            _refillRate = refillRate;
            _tokens = capacity;
            _lastRefill = DateTime.UtcNow;
        }

        public bool TryAcquire(int count = 1)
        {
            lock (_lock)
            {
                Refill();

                if (_tokens >= count)
                {
                    _tokens -= count;
                    return true;
                }

                return false;
            }
        }

        private void Refill()
        {
            var now = DateTime.UtcNow;
            var elapsed = (now - _lastRefill).TotalSeconds;
            var tokensToAdd = (int)(elapsed * _refillRate);

            if (tokensToAdd > 0)
            {
                _tokens = Math.Min(_capacity, _tokens + tokensToAdd);
                _lastRefill = now;
            }
        }

        public int AvailableTokens
        {
            get
            {
                lock (_lock)
                {
                    Refill();
                    return _tokens;
                }
            }
        }
    }

    /// <summary>
    /// Rate limiter manager for multiple keys.
    /// </summary>
    public class RateLimiterManager
    {
        private static readonly ConcurrentDictionary<string, TokenBucketRateLimiter> _limiters =
            new ConcurrentDictionary<string, TokenBucketRateLimiter>();

        public static bool TryAcquire(string key, int capacity, int refillRate)
        {
            var limiter = _limiters.GetOrAdd(key, _ => new TokenBucketRateLimiter(capacity, refillRate));
            return limiter.TryAcquire();
        }

        public static void Clear()
        {
            _limiters.Clear();
        }
    }
}
