using System;
using System.Collections.Generic;
using System.Net.Http;
using GetCNPJ;
using GetCNPJ.Cache;
using GetCNPJ.Interfaces;
using GetCNPJ.Providers.BrasilAPI;
using GetCNPJ.Providers.CNPJA;
using GetCNPJ.Providers.CNPJWS;
using GetCNPJ.Providers.ReceitaWS;
using GetCNPJ.RateLimiter;
using GetCNPJ.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GetCNPJ.Extensions
{
    /// <summary>
    /// Métodos de extensão para registro de serviços do GetCNPJ no IServiceCollection (Injeção de Dependência).
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adiciona os serviços do GetCNPJ ao container de Injeção de Dependência.
        /// </summary>
        /// <param name="services">A coleção de serviços.</param>
        /// <param name="configure">Ação para configurar as opções do CnpjClient.</param>
        /// <returns>A coleção de serviços para encadeamento.</returns>
        public static IServiceCollection AddGetCNPJ(
            this IServiceCollection services,
            Action<CnpjClientOptions>? configure = null)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));

            var options = new CnpjClientOptions();
            configure?.Invoke(options);

            services.AddSingleton(options);

            // Rate Limiter
            services.AddSingleton<IRateLimiter>(sp =>
                new SlidingWindowRateLimiter(options.MaxRequestsPerMinute, TimeSpan.FromMinutes(1)));

            // Cache
            if (options.EnableCache)
            {
                services.AddMemoryCache();
                services.AddSingleton<ICnpjCache>(sp =>
                {
                    var memoryCache = sp.GetRequiredService<IMemoryCache>();
                    return new MemoryCnpjCache(memoryCache, options.CacheTtl);
                });
            }

            // HTTP Client padrão
            services.AddHttpClient("GetCNPJ", client =>
            {
                client.Timeout = options.Timeout;
                if (!string.IsNullOrWhiteSpace(options.UserAgent))
                {
                    client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", options.UserAgent);
                }
            });

            // Provedores
            services.AddSingleton<ICnpjService>(sp =>
            {
                var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
                var httpClient = httpClientFactory.CreateClient("GetCNPJ");
                var rateLimiter = sp.GetRequiredService<IRateLimiter>();
                var cache = options.EnableCache ? sp.GetService<ICnpjCache>() : null;
                var logger = sp.GetService<ILogger<CnpjService>>();

                var providers = new List<ICnpjProvider>();

                if (options.EnableCNPJWS)
                    providers.Add(new CNPJWSProvider(httpClient, rateLimiter));

                if (options.EnableReceitaWS)
                    providers.Add(new ReceitaWSProvider(httpClient, rateLimiter));

                if (options.EnableBrasilAPI)
                    providers.Add(new BrasilAPIProvider(httpClient, rateLimiter));

                if (options.EnableCNPJA)
                    providers.Add(new CNPJAProvider(httpClient, rateLimiter));

                return new CnpjService(providers, cache, logger);
            });

            // Cliente
            services.AddTransient<CnpjClient>(sp =>
            {
                var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
                var httpClient = httpClientFactory.CreateClient("GetCNPJ");
                var cache = options.EnableCache ? sp.GetService<ICnpjCache>() : null;
                var logger = sp.GetService<ILogger<CnpjService>>();

                return new CnpjClient(httpClient, options, cache, logger);
            });

            return services;
        }
    }
}
