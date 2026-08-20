using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using GetCNPJ.Cache;
using GetCNPJ.Enums;
using GetCNPJ.Interfaces;
using GetCNPJ.Models;
using GetCNPJ.Providers.BrasilAPI;
using GetCNPJ.Providers.CNPJA;
using GetCNPJ.Providers.CNPJWS;
using GetCNPJ.Providers.ReceitaWS;
using GetCNPJ.RateLimiter;
using GetCNPJ.Services;
using Microsoft.Extensions.Logging;

namespace GetCNPJ
{
    /// <summary>
    /// Cliente principal para consulta de CNPJ.
    /// Fornece métodos assíncronos e síncronos, suporte a múltiplos provedores, rate limiting, cache e batching.
    /// </summary>
    public class CnpjClient : IDisposable
    {
        private readonly ICnpjService _cnpjService;
        private readonly HttpClient _httpClient;
        private readonly bool _disposeHttpClient;
        private readonly ICnpjCache? _cache;
        private readonly bool _disposeCache;

        /// <summary>
        /// Construtor com configuração padrão
        /// </summary>
        public CnpjClient() : this(null, null, null, null)
        {
        }

        /// <summary>
        /// Construtor com opções de configuração
        /// </summary>
        /// <param name="options">Opções de configuração</param>
        public CnpjClient(CnpjClientOptions options) : this(null, options, null, null)
        {
        }

        /// <summary>
        /// Construtor com HttpClient customizado e opções
        /// </summary>
        /// <param name="httpClient">HttpClient customizado (opcional)</param>
        /// <param name="options">Opções de configuração (opcional)</param>
        /// <param name="cache">Mecanismo de cache opcional (opcional)</param>
        /// <param name="logger">Logger opcional (opcional)</param>
        public CnpjClient(
            HttpClient? httpClient = null,
            CnpjClientOptions? options = null,
            ICnpjCache? cache = null,
            ILogger<CnpjService>? logger = null)
        {
            options = options ?? new CnpjClientOptions();

            // Configura HttpClient
            if (httpClient != null)
            {
                _httpClient = httpClient;
                _disposeHttpClient = false;
            }
            else
            {
                _httpClient = new HttpClient
                {
                    Timeout = options.Timeout
                };
                _disposeHttpClient = true;
            }

            // Define User-Agent padrão caso não esteja presente
            if (_httpClient.DefaultRequestHeaders.UserAgent.Count == 0 && !string.IsNullOrWhiteSpace(options.UserAgent))
            {
                _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", options.UserAgent);
            }

            // Configura Cache
            if (cache != null)
            {
                _cache = cache;
                _disposeCache = false;
            }
            else if (options.EnableCache)
            {
                _cache = new MemoryCnpjCache(options.CacheTtl);
                _disposeCache = true;
            }
            else
            {
                _cache = null;
                _disposeCache = false;
            }

            // Configura Rate Limiter
            var rateLimiter = new SlidingWindowRateLimiter(
                options.MaxRequestsPerMinute,
                TimeSpan.FromMinutes(1)
            );

            // Configura provedores
            var providers = new List<ICnpjProvider>();

            if (options.EnableCNPJWS)
            {
                providers.Add(new CNPJWSProvider(_httpClient, rateLimiter));
            }

            if (options.EnableReceitaWS)
            {
                providers.Add(new ReceitaWSProvider(_httpClient, rateLimiter));
            }

            if (options.EnableBrasilAPI)
            {
                providers.Add(new BrasilAPIProvider(_httpClient, rateLimiter));
            }

            if (options.EnableCNPJA)
            {
                providers.Add(new CNPJAProvider(_httpClient, rateLimiter));
            }

            if (providers.Count == 0)
            {
                throw new InvalidOperationException("Pelo menos um provedor deve estar habilitado nas opções.");
            }

            _cnpjService = new CnpjService(providers, _cache, logger);
        }

        /// <summary>
        /// Consulta dados de um CNPJ de forma assíncrona
        /// </summary>
        /// <param name="cnpj">CNPJ a ser consultado (pode conter formatação)</param>
        /// <param name="cancellationToken">Token de cancelamento</param>
        /// <returns>Resultado da consulta</returns>
        public Task<CnpjResult> GetAsync(string cnpj, CancellationToken cancellationToken = default)
        {
            return _cnpjService.GetCnpjAsync(cnpj, cancellationToken);
        }

        /// <summary>
        /// Consulta dados de um CNPJ usando um provedor específico
        /// </summary>
        /// <param name="cnpj">CNPJ a ser consultado</param>
        /// <param name="providerName">Nome do provedor (CNPJWS, ReceitaWS, BrasilAPI, CNPJA)</param>
        /// <param name="cancellationToken">Token de cancelamento</param>
        /// <returns>Resultado da consulta</returns>
        public Task<CnpjResult> GetFromProviderAsync(
            string cnpj,
            string providerName,
            CancellationToken cancellationToken = default)
        {
            return _cnpjService.GetCnpjFromProviderAsync(cnpj, providerName, cancellationToken);
        }

        /// <summary>
        /// Consulta dados de um CNPJ usando um provedor específico via enum fortemente tipado
        /// </summary>
        /// <param name="cnpj">CNPJ a ser consultado</param>
        /// <param name="provider">Tipo do provedor</param>
        /// <param name="cancellationToken">Token de cancelamento</param>
        /// <returns>Resultado da consulta</returns>
        public Task<CnpjResult> GetFromProviderAsync(
            string cnpj,
            ProviderType provider,
            CancellationToken cancellationToken = default)
        {
            return _cnpjService.GetCnpjFromProviderAsync(cnpj, provider, cancellationToken);
        }

        /// <summary>
        /// Consulta múltiplos CNPJs em lote de forma assíncrona
        /// </summary>
        /// <param name="cnpjs">Coleção de CNPJs</param>
        /// <param name="maxDegreeOfParallelism">Grau máximo de paralelismo</param>
        /// <param name="cancellationToken">Token de cancelamento</param>
        /// <returns>Lista com os resultados na ordem correspondente</returns>
        public Task<IReadOnlyList<CnpjResult>> GetBatchAsync(
            IEnumerable<string> cnpjs,
            int maxDegreeOfParallelism = 2,
            CancellationToken cancellationToken = default)
        {
            return _cnpjService.GetBatchAsync(cnpjs, maxDegreeOfParallelism, cancellationToken);
        }

        /// <summary>
        /// Consulta dados de um CNPJ de forma síncrona
        /// </summary>
        /// <param name="cnpj">CNPJ a ser consultado (pode conter formatação)</param>
        /// <returns>Resultado da consulta</returns>
        public CnpjResult Get(string cnpj)
        {
            return GetAsync(cnpj).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Consulta dados de um CNPJ usando um provedor específico de forma síncrona
        /// </summary>
        /// <param name="cnpj">CNPJ a ser consultado</param>
        /// <param name="providerName">Nome do provedor (CNPJWS, ReceitaWS, BrasilAPI, CNPJA)</param>
        /// <returns>Resultado da consulta</returns>
        public CnpjResult GetFromProvider(string cnpj, string providerName)
        {
            return GetFromProviderAsync(cnpj, providerName).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Consulta dados de um CNPJ usando um provedor específico via enum de forma síncrona
        /// </summary>
        /// <param name="cnpj">CNPJ a ser consultado</param>
        /// <param name="provider">Tipo do provedor</param>
        /// <returns>Resultado da consulta</returns>
        public CnpjResult GetFromProvider(string cnpj, ProviderType provider)
        {
            return GetFromProviderAsync(cnpj, provider).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Consulta múltiplos CNPJs em lote de forma síncrona
        /// </summary>
        /// <param name="cnpjs">Coleção de CNPJs</param>
        /// <param name="maxDegreeOfParallelism">Grau de paralelismo</param>
        /// <returns>Lista com os resultados</returns>
        public IReadOnlyList<CnpjResult> GetBatch(IEnumerable<string> cnpjs, int maxDegreeOfParallelism = 2)
        {
            return GetBatchAsync(cnpjs, maxDegreeOfParallelism).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Libera os recursos utilizados
        /// </summary>
        public void Dispose()
        {
            if (_disposeHttpClient)
            {
                _httpClient?.Dispose();
            }

            if (_disposeCache && _cache is IDisposable disposableCache)
            {
                disposableCache.Dispose();
            }
        }
    }

    /// <summary>
    /// Opções de configuração do CnpjClient
    /// </summary>
    public class CnpjClientOptions
    {
        /// <summary>
        /// Timeout para requisições HTTP (padrão: 30 segundos)
        /// </summary>
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Número máximo de requisições por minuto por provedor (padrão: 3)
        /// </summary>
        public int MaxRequestsPerMinute { get; set; } = 3;

        /// <summary>
        /// User-Agent enviado nas requisições HTTP
        /// </summary>
        public string UserAgent { get; set; } = "GetCNPJ-DotNet/1.1.0 (https://github.com/bruno-ferreira-tech/GetCNPJ)";

        /// <summary>
        /// Habilita o cache em memória de consultas de CNPJ (padrão: true)
        /// </summary>
        public bool EnableCache { get; set; } = true;

        /// <summary>
        /// Tempo de vida (TTL) do cache em memória (padrão: 12 horas)
        /// </summary>
        public TimeSpan CacheTtl { get; set; } = TimeSpan.FromHours(12);

        /// <summary>
        /// Habilita o provedor CNPJ.WS (padrão: true)
        /// Recomendado manter habilitado pois é o único que retorna inscrição estadual
        /// </summary>
        public bool EnableCNPJWS { get; set; } = true;

        /// <summary>
        /// Habilita o provedor ReceitaWS (padrão: true)
        /// </summary>
        public bool EnableReceitaWS { get; set; } = true;

        /// <summary>
        /// Habilita o provedor BrasilAPI (padrão: true)
        /// </summary>
        public bool EnableBrasilAPI { get; set; } = true;

        /// <summary>
        /// Habilita o provedor CNPJA (padrão: true)
        /// </summary>
        public bool EnableCNPJA { get; set; } = true;
    }
}
