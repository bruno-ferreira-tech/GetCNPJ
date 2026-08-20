using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GetCNPJ.Exceptions;
using GetCNPJ.Interfaces;
using GetCNPJ.Models;
using GetCNPJ.Utils;

namespace GetCNPJ.Providers.Base
{
    /// <summary>
    /// Classe base para provedores de dados de CNPJ
    /// </summary>
    public abstract class CnpjProviderBase : ICnpjProvider
    {
        protected static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        protected readonly HttpClient _httpClient;
        protected readonly IRateLimiter _rateLimiter;

        /// <inheritdoc/>
        public abstract string ProviderName { get; }

        /// <inheritdoc/>
        public abstract int Priority { get; }

        /// <summary>
        /// URL base da API
        /// </summary>
        protected abstract string BaseUrl { get; }

        protected CnpjProviderBase(HttpClient httpClient, IRateLimiter rateLimiter)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _rateLimiter = rateLimiter ?? throw new ArgumentNullException(nameof(rateLimiter));
        }

        /// <inheritdoc/>
        public async Task<CnpjData?> GetCnpjDataAsync(string cnpj, CancellationToken cancellationToken = default)
        {
            try
            {
                // Valida e normaliza o CNPJ
                cnpj = ValidateAndNormalizeCnpj(cnpj);

                // Aguarda rate limiter
                await _rateLimiter.WaitIfNeededAsync(ProviderName, cancellationToken).ConfigureAwait(false);

                // Registra a requisição
                _rateLimiter.RecordRequest(ProviderName);

                // Faz a requisição
                var data = await FetchDataAsync(cnpj, cancellationToken).ConfigureAwait(false);

                if (data != null)
                {
                    data.Provedor = ProviderName;
                }

                return data;
            }
            catch (InvalidCnpjException)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new ProviderException(ProviderName, $"Erro ao consultar CNPJ: {ex.Message}", ex);
            }
        }

        /// <inheritdoc/>
        public virtual async Task<bool> IsAvailableAsync()
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var request = new HttpRequestMessage(HttpMethod.Head, BaseUrl);
                var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token).ConfigureAwait(false);
                return (int)response.StatusCode < 500;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Método abstrato para buscar dados do provedor específico
        /// </summary>
        protected abstract Task<CnpjData?> FetchDataAsync(string cnpj, CancellationToken cancellationToken);

        /// <summary>
        /// Valida e normaliza um CNPJ (remove formatação)
        /// </summary>
        protected string ValidateAndNormalizeCnpj(string cnpj)
        {
            if (string.IsNullOrWhiteSpace(cnpj))
                throw new InvalidCnpjException("CNPJ não pode ser nulo ou vazio");

            var normalized = CnpjValidator.Normalize(cnpj);

            if (normalized.Length != 14)
                throw new InvalidCnpjException($"CNPJ deve ter 14 dígitos. Recebido: {cnpj}");

            if (!CnpjValidator.IsValid(normalized))
                throw new InvalidCnpjException($"CNPJ inválido: {cnpj}");

            return normalized;
        }

        /// <summary>
        /// Valida os dígitos verificadores do CNPJ
        /// </summary>
        protected bool IsValidCnpj(string cnpj)
        {
            return CnpjValidator.IsValid(cnpj);
        }

        /// <summary>
        /// Formata um CNPJ com pontuação
        /// </summary>
        protected string FormatCnpj(string cnpj)
        {
            return CnpjValidator.Format(cnpj);
        }
    }
}
