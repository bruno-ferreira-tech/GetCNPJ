namespace GetCNPJ.Enums
{
    /// <summary>
    /// Tipos de provedores de dados de CNPJ
    /// </summary>
    public enum ProviderType
    {
        /// <summary>
        /// CNPJ.WS - https://publica.cnpj.ws (Provedor padrão, inclui Inscrição Estadual)
        /// </summary>
        CNPJWS,

        /// <summary>
        /// ReceitaWS - https://receitaws.com.br
        /// </summary>
        ReceitaWS,

        /// <summary>
        /// BrasilAPI - https://brasilapi.com.br
        /// </summary>
        BrasilAPI,

        /// <summary>
        /// CNPJA - https://open.cnpja.com
        /// </summary>
        CNPJA
    }
}
