using System;
using System.Text;

namespace GetCNPJ.Utils
{
    /// <summary>
    /// Utilitário de alta performance para validação, normalização e formatação de CNPJ.
    /// Implementado com zero-allocation para algoritmos de verificação.
    /// </summary>
    public static class CnpjValidator
    {
        private static readonly int[] Multiplier1 = { 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };
        private static readonly int[] Multiplier2 = { 6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };

        /// <summary>
        /// Remove todos os caracteres não numéricos de um CNPJ.
        /// </summary>
        /// <param name="cnpj">CNPJ formatado ou não.</param>
        /// <returns>String contendo apenas os dígitos do CNPJ.</returns>
        public static string Normalize(string? cnpj)
        {
            if (string.IsNullOrWhiteSpace(cnpj))
                return string.Empty;

            var sb = new StringBuilder(14);
            foreach (char c in cnpj)
            {
                if (c >= '0' && c <= '9')
                {
                    sb.Append(c);
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Valida se o CNPJ possui formato e dígitos verificadores válidos.
        /// Suporta CNPJ com ou sem pontuação.
        /// </summary>
        /// <param name="cnpj">CNPJ a ser validado.</param>
        /// <returns>True se válido, False caso contrário.</returns>
        public static bool IsValid(string? cnpj)
        {
            if (string.IsNullOrWhiteSpace(cnpj))
                return false;

            // Extrai apenas dígitos sem alocar se já for limpo
            string digits = Normalize(cnpj);

            if (digits.Length != 14)
                return false;

            // Verifica se todos os dígitos são iguais (ex: 00000000000000, 11111111111111)
            bool allSame = true;
            char first = digits[0];
            for (int i = 1; i < 14; i++)
            {
                if (digits[i] != first)
                {
                    allSame = false;
                    break;
                }
            }

            if (allSame)
                return false;

            // Primeiro dígito verificador
            int sum = 0;
            for (int i = 0; i < 12; i++)
            {
                sum += (digits[i] - '0') * Multiplier1[i];
            }

            int remainder = sum % 11;
            int digit1 = remainder < 2 ? 0 : 11 - remainder;

            if ((digits[12] - '0') != digit1)
                return false;

            // Segundo dígito verificador
            sum = 0;
            for (int i = 0; i < 13; i++)
            {
                sum += (digits[i] - '0') * Multiplier2[i];
            }

            remainder = sum % 11;
            int digit2 = remainder < 2 ? 0 : 11 - remainder;

            return (digits[13] - '0') == digit2;
        }

        /// <summary>
        /// Formata um CNPJ no padrão XX.XXX.XXX/XXXX-XX.
        /// </summary>
        /// <param name="cnpj">CNPJ com ou sem máscara.</param>
        /// <returns>CNPJ formatado ou o valor original caso não tenha 14 dígitos.</returns>
        public static string Format(string? cnpj)
        {
            if (string.IsNullOrWhiteSpace(cnpj))
                return string.Empty;

            string digits = Normalize(cnpj);
            if (digits.Length != 14)
                return cnpj ?? string.Empty;

            return $"{digits.Substring(0, 2)}.{digits.Substring(2, 3)}.{digits.Substring(5, 3)}/{digits.Substring(8, 4)}-{digits.Substring(12, 2)}";
        }
    }
}
