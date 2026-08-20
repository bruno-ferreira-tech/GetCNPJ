using System;
using System.Threading;
using System.Threading.Tasks;
using GetCNPJ.Models;

namespace GetCNPJ.Interfaces
{
    /// <summary>
    /// Contrato para cache de consultas de CNPJ.
    /// </summary>
    public interface ICnpjCache
    {
        /// <summary>
        /// Obtém os dados de um CNPJ do cache, se disponível.
        /// </summary>
        /// <param name="cnpj">CNPJ (limpo ou formatado).</param>
        /// <param name="cancellationToken">Token de cancelamento.</param>
        /// <returns>Dados do CNPJ ou null caso não esteja em cache.</returns>
        Task<CnpjData?> GetAsync(string cnpj, CancellationToken cancellationToken = default);

        /// <summary>
        /// Armazena os dados de um CNPJ no cache.
        /// </summary>
        /// <param name="cnpj">CNPJ (limpo ou formatado).</param>
        /// <param name="data">Dados a serem armazenados.</param>
        /// <param name="ttl">Tempo de vida opcional do registro em cache.</param>
        /// <param name="cancellationToken">Token de cancelamento.</param>
        Task SetAsync(string cnpj, CnpjData data, TimeSpan? ttl = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Remove um registro específico do cache.
        /// </summary>
        /// <param name="cnpj">CNPJ a ser removido.</param>
        /// <param name="cancellationToken">Token de cancelamento.</param>
        Task RemoveAsync(string cnpj, CancellationToken cancellationToken = default);

        /// <summary>
        /// Limpa todos os registros do cache.
        /// </summary>
        /// <param name="cancellationToken">Token de cancelamento.</param>
        Task ClearAsync(CancellationToken cancellationToken = default);
    }
}
