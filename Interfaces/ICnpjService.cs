using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GetCNPJ.Enums;
using GetCNPJ.Models;

namespace GetCNPJ.Interfaces
{
    /// <summary>
    /// Interface do serviço principal de consulta de CNPJ
    /// </summary>
    public interface ICnpjService
    {
        /// <summary>
        /// Consulta dados de um CNPJ utilizando os provedores disponíveis (Chain of Responsibility)
        /// </summary>
        /// <param name="cnpj">CNPJ a ser consultado (pode conter formatação)</param>
        /// <param name="cancellationToken">Token de cancelamento</param>
        /// <returns>Resultado da consulta</returns>
        Task<CnpjResult> GetCnpjAsync(string cnpj, CancellationToken cancellationToken = default);

        /// <summary>
        /// Consulta dados de um CNPJ utilizando um provedor específico
        /// </summary>
        /// <param name="cnpj">CNPJ a ser consultado</param>
        /// <param name="providerName">Nome do provedor (CNPJWS, ReceitaWS, BrasilAPI, CNPJA)</param>
        /// <param name="cancellationToken">Token de cancelamento</param>
        /// <returns>Resultado da consulta</returns>
        Task<CnpjResult> GetCnpjFromProviderAsync(string cnpj, string providerName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Consulta dados de um CNPJ utilizando um provedor específico via enum fortemente tipado
        /// </summary>
        /// <param name="cnpj">CNPJ a ser consultado</param>
        /// <param name="provider">Tipo do provedor</param>
        /// <param name="cancellationToken">Token de cancelamento</param>
        /// <returns>Resultado da consulta</returns>
        Task<CnpjResult> GetCnpjFromProviderAsync(string cnpj, ProviderType provider, CancellationToken cancellationToken = default);

        /// <summary>
        /// Consulta uma lista de CNPJs em lote de forma controlada
        /// </summary>
        /// <param name="cnpjs">Coleção de CNPJs a serem consultados</param>
        /// <param name="maxDegreeOfParallelism">Grau máximo de paralelismo (padrão: 2 para respeitar rate limits)</param>
        /// <param name="cancellationToken">Token de cancelamento</param>
        /// <returns>Lista de resultados na ordem das consultas</returns>
        Task<IReadOnlyList<CnpjResult>> GetBatchAsync(IEnumerable<string> cnpjs, int maxDegreeOfParallelism = 2, CancellationToken cancellationToken = default);
    }
}
