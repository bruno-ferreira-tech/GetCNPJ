using System;
using System.Threading;
using System.Threading.Tasks;
using GetCNPJ.Interfaces;
using GetCNPJ.Models;
using GetCNPJ.Utils;
using Microsoft.Extensions.Caching.Memory;

namespace GetCNPJ.Cache
{
    /// <summary>
    /// Implementação padrão de cache em memória usando IMemoryCache.
    /// </summary>
    public class MemoryCnpjCache : ICnpjCache, IDisposable
    {
        private readonly IMemoryCache _memoryCache;
        private readonly bool _ownsCache;
        private readonly TimeSpan _defaultTtl;

        /// <summary>
        /// Construtor com IMemoryCache injetado.
        /// </summary>
        /// <param name="memoryCache">Instância de IMemoryCache.</param>
        /// <param name="defaultTtl">Tempo padrão de expiração do cache (padrão: 12 horas).</param>
        public MemoryCnpjCache(IMemoryCache memoryCache, TimeSpan? defaultTtl = null)
        {
            _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
            _ownsCache = false;
            _defaultTtl = defaultTtl ?? TimeSpan.FromHours(12);
        }

        /// <summary>
        /// Construtor com criação interna de MemoryCache.
        /// </summary>
        /// <param name="defaultTtl">Tempo padrão de expiração do cache (padrão: 12 horas).</param>
        public MemoryCnpjCache(TimeSpan? defaultTtl = null)
        {
            _memoryCache = new MemoryCache(new MemoryCacheOptions());
            _ownsCache = true;
            _defaultTtl = defaultTtl ?? TimeSpan.FromHours(12);
        }

        private static string GetCacheKey(string cnpj)
        {
            var normalized = CnpjValidator.Normalize(cnpj);
            return $"GetCNPJ_{normalized}";
        }

        /// <inheritdoc/>
        public Task<CnpjData?> GetAsync(string cnpj, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var key = GetCacheKey(cnpj);

            if (_memoryCache.TryGetValue(key, out CnpjData? cachedData))
            {
                return Task.FromResult(cachedData);
            }

            return Task.FromResult<CnpjData?>(null);
        }

        /// <inheritdoc/>
        public Task SetAsync(string cnpj, CnpjData data, TimeSpan? ttl = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (data == null) return Task.CompletedTask;

            var key = GetCacheKey(cnpj);
            var expiration = ttl ?? _defaultTtl;

            var entryOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiration
            };

            _memoryCache.Set(key, data, entryOptions);
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public Task RemoveAsync(string cnpj, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var key = GetCacheKey(cnpj);
            _memoryCache.Remove(key);
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public Task ClearAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_memoryCache is MemoryCache concreteCache)
            {
                concreteCache.Compact(1.0); // Remove 100% dos itens
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_ownsCache && _memoryCache is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }
}
