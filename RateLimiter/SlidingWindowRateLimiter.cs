using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GetCNPJ.Interfaces;

namespace GetCNPJ.RateLimiter
{
    /// <summary>
    /// Implementação de Rate Limiter usando Sliding Window com thread-safety completo.
    /// Limita o número de requisições por intervalo de tempo para cada provedor.
    /// </summary>
    public class SlidingWindowRateLimiter : IRateLimiter
    {
        private readonly int _maxRequests;
        private readonly TimeSpan _timeWindow;
        private readonly ConcurrentDictionary<string, ProviderRateLimitState> _providerStates;

        /// <summary>
        /// Construtor
        /// </summary>
        /// <param name="maxRequests">Número máximo de requisições no intervalo</param>
        /// <param name="timeWindow">Intervalo de tempo (padrão: 1 minuto)</param>
        public SlidingWindowRateLimiter(int maxRequests = 3, TimeSpan? timeWindow = null)
        {
            if (maxRequests <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxRequests), "O número máximo de requisições deve ser maior que zero.");

            _maxRequests = maxRequests;
            _timeWindow = timeWindow ?? TimeSpan.FromMinutes(1);
            _providerStates = new ConcurrentDictionary<string, ProviderRateLimitState>(StringComparer.OrdinalIgnoreCase);
        }

        private ProviderRateLimitState GetState(string providerName)
        {
            return _providerStates.GetOrAdd(providerName, _ => new ProviderRateLimitState());
        }

        /// <inheritdoc/>
        public async Task WaitIfNeededAsync(string providerName, CancellationToken cancellationToken = default)
        {
            var state = GetState(providerName);

            await state.Semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    TimeSpan waitTime = TimeSpan.Zero;

                    lock (state.LockObj)
                    {
                        CleanOldTimestamps(state.Timestamps);

                        if (state.Timestamps.Count < _maxRequests)
                        {
                            // Slot disponível
                            return;
                        }

                        var oldestRequest = state.Timestamps.Peek();
                        waitTime = oldestRequest.Add(_timeWindow) - DateTime.UtcNow;
                    }

                    if (waitTime > TimeSpan.Zero)
                    {
                        await Task.Delay(waitTime, cancellationToken).ConfigureAwait(false);
                    }
                }
            }
            finally
            {
                state.Semaphore.Release();
            }
        }

        /// <inheritdoc/>
        public void RecordRequest(string providerName)
        {
            var state = GetState(providerName);
            lock (state.LockObj)
            {
                CleanOldTimestamps(state.Timestamps);
                state.Timestamps.Enqueue(DateTime.UtcNow);
            }
        }

        /// <inheritdoc/>
        public void Reset(string providerName)
        {
            if (_providerStates.TryGetValue(providerName, out var state))
            {
                lock (state.LockObj)
                {
                    state.Timestamps.Clear();
                }
            }
        }

        /// <inheritdoc/>
        public int GetAvailableRequests(string providerName)
        {
            if (!_providerStates.TryGetValue(providerName, out var state))
                return _maxRequests;

            lock (state.LockObj)
            {
                CleanOldTimestamps(state.Timestamps);
                return Math.Max(0, _maxRequests - state.Timestamps.Count);
            }
        }

        private void CleanOldTimestamps(Queue<DateTime> timestamps)
        {
            var cutoffTime = DateTime.UtcNow.Subtract(_timeWindow);

            while (timestamps.Count > 0 && timestamps.Peek() <= cutoffTime)
            {
                timestamps.Dequeue();
            }
        }

        private sealed class ProviderRateLimitState
        {
            public SemaphoreSlim Semaphore { get; } = new SemaphoreSlim(1, 1);
            public object LockObj { get; } = new object();
            public Queue<DateTime> Timestamps { get; } = new Queue<DateTime>();
        }
    }
}
