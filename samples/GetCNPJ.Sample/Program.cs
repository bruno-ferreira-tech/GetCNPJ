using System;
using System.Diagnostics;
using System.Threading.Tasks;
using GetCNPJ;
using GetCNPJ.Enums;
using GetCNPJ.Extensions;
using GetCNPJ.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GetCNPJ.Sample
{
    internal class Program
    {
        private static async Task Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine("╔═════════════════════════════════════════════════════════╗");
            Console.WriteLine("║        GetCNPJ - Exemplos de Uso e Demonstração         ║");
            Console.WriteLine("╚═════════════════════════════════════════════════════════╝\n");

            // 1. Exemplo Básico Assíncrono
            await ExemploConsultaBasica();

            // 2. Exemplo com Provedor Específico (Tipado via Enum)
            await ExemploProvedorTipado();

            // 3. Exemplo de Cache em Memória
            await ExemploCache();

            // 4. Exemplo de Consulta em Lote (Batch)
            await ExemploConsultaEmLote();

            // 5. Exemplo com Injeção de Dependência (DI)
            await ExemploInjecaoDependencia();

            Console.WriteLine("\n✓ Todos os exemplos foram concluídos.");
        }

        private static async Task ExemploConsultaBasica()
        {
            Console.WriteLine("--- 1. Exemplo de Consulta Básica ---");
            using var client = new CnpjClient();

            var result = await client.GetAsync("03.312.791/0001-83");
            if (result.Success && result.Data != null)
            {
                var d = result.Data;
                Console.WriteLine($"Razão Social: {d.RazaoSocial}");
                Console.WriteLine($"Nome Fantasia: {d.NomeFantasia}");
                Console.WriteLine($"CNPJ Formatado: {d.Cnpj}");
                Console.WriteLine($"Situação: {d.Situacao}");
                Console.WriteLine($"Endereço: {d.Endereco?.EnderecoCompleto}");
                Console.WriteLine($"Provedor Utilizado: {d.Provedor}\n");
            }
            else
            {
                Console.WriteLine($"Falha: {result.ErrorMessage}\n");
            }
        }

        private static async Task ExemploProvedorTipado()
        {
            Console.WriteLine("--- 2. Consulta com Provedor Específico (Enum Tipado) ---");
            using var client = new CnpjClient();

            // Consulta diretamente pelo CNPJWS (único com Inscrição Estadual)
            var result = await client.GetFromProviderAsync("03312791000183", ProviderType.CNPJWS);

            if (result.Success && result.Data != null)
            {
                Console.WriteLine($"[CNPJ.WS] Razão Social: {result.Data.RazaoSocial}");
                if (result.Data.InscricoesEstaduais?.Count > 0)
                {
                    Console.WriteLine("Inscrições Estaduais:");
                    foreach (var ie in result.Data.InscricoesEstaduais)
                    {
                        Console.WriteLine($"  - {ie}");
                    }
                }
            }
            else
            {
                Console.WriteLine($"Falha ao consultar via CNPJ.WS: {result.ErrorMessage}");
            }
            Console.WriteLine();
        }

        private static async Task ExemploCache()
        {
            Console.WriteLine("--- 3. Demonstração de Cache em Memória ---");
            var options = new CnpjClientOptions
            {
                EnableCache = true,
                CacheTtl = TimeSpan.FromMinutes(30)
            };

            using var client = new CnpjClient(options);
            var cnpj = "03.312.791/0001-83";

            // Primeira consulta: faz a requisição HTTP
            var sw = Stopwatch.StartNew();
            var r1 = await client.GetAsync(cnpj);
            sw.Stop();
            Console.WriteLine($"1ª Consulta (API): {r1.Data?.RazaoSocial} - Tempo: {sw.ElapsedMilliseconds}ms");

            // Segunda consulta: recuperada instantaneamente do cache em memória
            sw.Restart();
            var r2 = await client.GetAsync(cnpj);
            sw.Stop();
            Console.WriteLine($"2ª Consulta (Cache): {r2.Data?.RazaoSocial} - Tempo: {sw.ElapsedMilliseconds}ms\n");
        }

        private static async Task ExemploConsultaEmLote()
        {
            Console.WriteLine("--- 4. Consulta em Lote (Batch Query) ---");
            using var client = new CnpjClient();

            string[] cnpjs = {
                "03312791000183", // SOS System
                "00000000000191", // Banco do Brasil
                "18236120000158"  // Nubank
            };

            Console.WriteLine($"Consultando {cnpjs.Length} CNPJs com controle de paralelismo...");
            var results = await client.GetBatchAsync(cnpjs, maxDegreeOfParallelism: 2);

            for (int i = 0; i < results.Count; i++)
            {
                if (results[i].Success)
                {
                    Console.WriteLine($"[{i + 1}] ✓ {results[i].Data?.Cnpj} - {results[i].Data?.RazaoSocial} ({results[i].Data?.Provedor})");
                }
                else
                {
                    Console.WriteLine($"[{i + 1}] ✗ {results[i].ErrorMessage}");
                }
            }
            Console.WriteLine();
        }

        private static async Task ExemploInjecaoDependencia()
        {
            Console.WriteLine("--- 5. Injeção de Dependência (DI com IServiceCollection) ---");

            var services = new ServiceCollection();

            // Configura logging no console
            services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));

            // Registra GetCNPJ
            services.AddGetCNPJ(options =>
            {
                options.MaxRequestsPerMinute = 3;
                options.EnableCache = true;
                options.CacheTtl = TimeSpan.FromHours(1);
            });

            var serviceProvider = services.BuildServiceProvider();

            // Resolve ICnpjService ou CnpjClient
            var cnpjService = serviceProvider.GetRequiredService<ICnpjService>();
            var result = await cnpjService.GetCnpjAsync("03312791000183");

            if (result.Success && result.Data != null)
            {
                Console.WriteLine($"Resolvido via DI -> Empresa: {result.Data.RazaoSocial} | Provedor: {result.Data.Provedor}");
            }
            Console.WriteLine();
        }
    }
}
