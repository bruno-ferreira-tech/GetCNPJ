using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GetCNPJ.Enums;
using GetCNPJ.Interfaces;
using GetCNPJ.Models;
using GetCNPJ.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace GetCNPJ.Services
{
    /// <summary>
    /// Serviço principal de consulta de CNPJ.
    /// Implementa Chain of Responsibility para tentar múltiplos provedores com suporte a Cache, Batch e Logging.
    /// </summary>
    public class CnpjService : ICnpjService
    {
        private readonly IEnumerable<ICnpjProvider> _providers;
        private readonly ICnpjCache? _cache;
        private readonly ILogger<CnpjService> _logger;

        /// <summary>
        /// Construtor principal
        /// </summary>
        /// <param name="providers">Lista de provedores disponíveis</param>
        /// <param name="cache">Serviço de cache opcional</param>
        /// <param name="logger">Logger opcional</param>
        public CnpjService(
            IEnumerable<ICnpjProvider> providers,
            ICnpjCache? cache = null,
            ILogger<CnpjService>? logger = null)
        {
            _providers = providers?.OrderBy(p => p.Priority)
                ?? throw new ArgumentNullException(nameof(providers));

            if (!_providers.Any())
            {
                throw new ArgumentException("Pelo menos um provedor deve ser configurado.", nameof(providers));
            }

            _cache = cache;
            _logger = logger ?? NullLogger<CnpjService>.Instance;
        }

        /// <inheritdoc/>
        public async Task<CnpjResult> GetCnpjAsync(string cnpj, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(cnpj))
            {
                return CnpjResult.CreateError("CNPJ não pode ser nulo ou vazio.");
            }

            var cleanCnpj = CnpjValidator.Normalize(cnpj);
            if (!CnpjValidator.IsValid(cleanCnpj))
            {
                _logger.LogWarning("Tentativa de consulta com CNPJ inválido: {Cnpj}", cnpj);
                return CnpjResult.CreateError($"CNPJ inválido: {cnpj}");
            }

            // 1. Tenta recuperar do Cache
            if (_cache != null)
            {
                try
                {
                    var cached = await _cache.GetAsync(cleanCnpj, cancellationToken).ConfigureAwait(false);
                    if (cached != null)
                    {
                        _logger.LogDebug("CNPJ {Cnpj} recuperado do cache.", cleanCnpj);
                        return CnpjResult.CreateSuccess(cached);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Erro ao consultar cache para CNPJ {Cnpj}", cleanCnpj);
                }
            }

            var errors = new List<ProviderError>();

            // 2. Chain of Responsibility: Tenta cada provedor em ordem de prioridade
            foreach (var provider in _providers)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    _logger.LogDebug("Consultando CNPJ {Cnpj} no provedor {ProviderName}...", cleanCnpj, provider.ProviderName);
                    var data = await provider.GetCnpjDataAsync(cleanCnpj, cancellationToken).ConfigureAwait(false);

                    if (data != null)
                    {
                        _logger.LogInformation("CNPJ {Cnpj} consultado com sucesso via {ProviderName}.", cleanCnpj, provider.ProviderName);

                        // Salva no Cache
                        if (_cache != null)
                        {
                            try
                            {
                                await _cache.SetAsync(cleanCnpj, data, cancellationToken: cancellationToken).ConfigureAwait(false);
                            }
                            catch (Exception cacheEx)
                            {
                                _logger.LogWarning(cacheEx, "Erro ao gravar CNPJ {Cnpj} no cache.", cleanCnpj);
                            }
                        }

                        return CnpjResult.CreateSuccess(data);
                    }

                    // Provedor retornou null (CNPJ não encontrado)
                    _logger.LogDebug("CNPJ {Cnpj} não encontrado no provedor {ProviderName}.", cleanCnpj, provider.ProviderName);
                    errors.Add(new ProviderError
                    {
                        ProviderName = provider.ProviderName,
                        ErrorMessage = "CNPJ não encontrado"
                    });
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Erro no provedor {ProviderName} ao consultar CNPJ {Cnpj}: {Message}", provider.ProviderName, cleanCnpj, ex.Message);
                    errors.Add(new ProviderError
                    {
                        ProviderName = provider.ProviderName,
                        ErrorMessage = ex.Message,
                        Exception = ex
                    });
                }
            }

            // Todos os provedores falharam
            var errorMessage = $"Não foi possível consultar o CNPJ. Tentativas: {errors.Count}";
            _logger.LogError("Falha ao consultar CNPJ {Cnpj}. Todos os {Count} provedores falharam.", cleanCnpj, errors.Count);
            return CnpjResult.CreateError(errorMessage, errors);
        }

        /// <inheritdoc/>
        public async Task<CnpjResult> GetCnpjFromProviderAsync(
            string cnpj,
            string providerName,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(cnpj))
                return CnpjResult.CreateError("CNPJ não pode ser nulo ou vazio.");

            var cleanCnpj = CnpjValidator.Normalize(cnpj);
            if (!CnpjValidator.IsValid(cleanCnpj))
                return CnpjResult.CreateError($"CNPJ inválido: {cnpj}");

            var provider = _providers.FirstOrDefault(p =>
                p.ProviderName.Equals(providerName, StringComparison.OrdinalIgnoreCase));

            if (provider == null)
            {
                return CnpjResult.CreateError(
                    $"Provedor '{providerName}' não encontrado. Provedores disponíveis: {string.Join(", ", _providers.Select(p => p.ProviderName))}"
                );
            }

            try
            {
                var data = await provider.GetCnpjDataAsync(cleanCnpj, cancellationToken).ConfigureAwait(false);

                if (data != null)
                {
                    return CnpjResult.CreateSuccess(data);
                }

                return CnpjResult.CreateError("CNPJ não encontrado");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                var error = new ProviderError
                {
                    ProviderName = provider.ProviderName,
                    ErrorMessage = ex.Message,
                    Exception = ex
                };

                return CnpjResult.CreateError($"Erro ao consultar CNPJ: {ex.Message}", new List<ProviderError> { error });
            }
        }

        /// <inheritdoc/>
        public Task<CnpjResult> GetCnpjFromProviderAsync(
            string cnpj,
            ProviderType provider,
            CancellationToken cancellationToken = default)
        {
            return GetCnpjFromProviderAsync(cnpj, provider.ToString(), cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<CnpjResult>> GetBatchAsync(
            IEnumerable<string> cnpjs,
            int maxDegreeOfParallelism = 2,
            CancellationToken cancellationToken = default)
        {
            if (cnpjs == null) throw new ArgumentNullException(nameof(cnpjs));

            var cnpjList = cnpjs.ToList();
            if (cnpjList.Count == 0) return Array.Empty<CnpjResult>();

            maxDegreeOfParallelism = Math.Max(1, maxDegreeOfParallelism);
            var results = new CnpjResult[cnpjList.Count];
            var throttler = new SemaphoreSlim(maxDegreeOfParallelism);

            var tasks = cnpjList.Select(async (cnpj, index) =>
            {
                await throttler.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    results[index] = await GetCnpjAsync(cnpj, cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    throttler.Release();
                }
            });

            await Task.WhenAll(tasks).ConfigureAwait(false);
            return results;
        }
    }
}
