# GetCNPJ

[![NuGet](https://img.shields.io/nuget/v/GetCNPJ.svg)](https://www.nuget.org/packages/GetCNPJ/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

<img width="374" height="373" alt="image" src="https://github.com/user-attachments/assets/a52c4ae9-fd76-48a6-af33-1dda1de6765d" />

Biblioteca .NET de alta performance para consulta de CNPJ em APIs públicas brasileiras, com suporte a múltiplos provedores, fallback automático (*Chain of Responsibility*), rate limiting thread-safe, cache em memória e injeção de dependência.

---

## 📋 Características

- ✅ **Multi-Target**: Suporta .NET Framework 4.8, 4.8.1, .NET 8, .NET 9 e .NET Standard 2.0.
- 🔄 **Chain of Responsibility**: Fallback automático e transparente caso um provedor falhe ou atinja cota.
- ⏱️ **Rate Limiting Thread-Safe**: Algoritmo *Sliding Window* seguro para ambientes concorrentes/multithread.
- 💾 **Cache em Memória Integrado**: `ICnpjCache` com TTL configurável para respostas instantâneas e economia de cota de API.
- 💉 **Injeção de Dependência (`Microsoft.Extensions.DependencyInjection`)**: Integração nativa com `services.AddGetCNPJ()` e `IHttpClientFactory`.
- 📦 **Consulta em Lote (*Batch Query*)**: Suporte a consultas paralelas de listas de CNPJs com controle de concorrência.
- 🔌 **Múltiplos Provedores Suportados**:
  - **CNPJ.WS** (https://publica.cnpj.ws) - **Provedor padrão (Prioridade 1)** ⭐ *Retorna Inscrição Estadual*
  - **ReceitaWS** (https://receitaws.com.br) - *Prioridade 2*
  - **BrasilAPI** (https://brasilapi.com.br) - *Prioridade 3*
  - **CNPJA** (https://open.cnpja.com) - *Prioridade 4*
- 🎯 **Retorno Padronizado**: Dados normalizados em DTOs consistentes independente do provedor.
- ⚡ **Zero-Allocation Validator**: `CnpjValidator` de alta performance sem alocação desnecessária de strings.
- 🛡️ **Async/Await Nativo**: Totalmente assíncrono com suporte a `CancellationToken` e métodos síncronos legados.

---

## 📦 Instalação

```bash
dotnet add package GetCNPJ
```

Ou via NuGet Package Manager:

```powershell
Install-Package GetCNPJ
```

---

## 🚀 Como Usar

### 1. Injeção de Dependência (ASP.NET Core / Worker Services)

A maneira recomendada em aplicações modernas é registrar o GetCNPJ no `IServiceCollection`:

```csharp
// Program.cs
using GetCNPJ.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Registra o GetCNPJ com opções customizadas
builder.Services.AddGetCNPJ(options =>
{
    options.MaxRequestsPerMinute = 3;
    options.EnableCache = true;
    options.CacheTtl = TimeSpan.FromHours(12);
    options.EnableCNPJWS = true;
});

var app = builder.Build();

// Usando em um Endpoint ou Controller:
app.MapGet("/empresa/{cnpj}", async (string cnpj, ICnpjService cnpjService) =>
{
    var result = await cnpjService.GetCnpjAsync(cnpj);
    return result.Success ? Results.Ok(result.Data) : Results.NotFound(result.ErrorMessage);
});

app.Run();
```

---

### 2. Uso Direto com `CnpjClient`

```csharp
using GetCNPJ;
using System;

// Instanciação simples (utiliza cache e rate limiter integrados)
using var client = new CnpjClient();

// Consulta (aceita formatado ou apenas números)
var result = await client.GetAsync("03.312.791/0001-83");

if (result.Success && result.Data != null)
{
    var data = result.Data;
    Console.WriteLine($"Razão Social: {data.RazaoSocial}");
    Console.WriteLine($"Nome Fantasia: {data.NomeFantasia}");
    Console.WriteLine($"CNPJ: {data.Cnpj}");
    Console.WriteLine($"Situação: {data.Situacao}");
    Console.WriteLine($"Endereço: {data.Endereco?.EnderecoCompleto}");
    Console.WriteLine($"Provedor Utilizado: {data.Provedor}");

    if (data.InscricoesEstaduais?.Count > 0)
    {
        Console.WriteLine("Inscrições Estaduais:");
        foreach (var ie in data.InscricoesEstaduais)
        {
            Console.WriteLine($"  - {ie}");
        }
    }
}
else
{
    Console.WriteLine($"Erro: {result.ErrorMessage}");
}
```

---

### 3. Consulta com Provedor Específico (Enum Tipado)

Você pode forçar a consulta em um provedor específico usando o enum `ProviderType`:

```csharp
using GetCNPJ;
using GetCNPJ.Enums;

using var client = new CnpjClient();

// Consulta diretamente no CNPJ.WS (único que traz Inscrição Estadual)
var result = await client.GetFromProviderAsync("03312791000183", ProviderType.CNPJWS);

if (result.Success)
{
    Console.WriteLine($"Razão Social: {result.Data.RazaoSocial}");
}
```

---

### 4. Consulta em Lote (*Batch Query*)

```csharp
using GetCNPJ;

using var client = new CnpjClient();

string[] cnpjs = {
    "03312791000183",
    "00000000000191",
    "18236120000158"
};

// Executa em paralelo respeitando o grau máximo de concorrência e o rate limiter
var results = await client.GetBatchAsync(cnpjs, maxDegreeOfParallelism: 2);

foreach (var res in results)
{
    if (res.Success)
        Console.WriteLine($"✓ {res.Data.Cnpj} - {res.Data.RazaoSocial}");
    else
        Console.WriteLine($"✗ {res.ErrorMessage}");
}
```

---

### 5. Validação e Formatação de Alta Performance (`CnpjValidator`)

Você pode usar o utilitário estático `CnpjValidator` de forma avulsa:

```csharp
using GetCNPJ.Utils;

bool valido = CnpjValidator.IsValid("03.312.791/0001-83"); // true
string limpo = CnpjValidator.Normalize("03.312.791/0001-83"); // "03312791000183"
string formatado = CnpjValidator.Format("03312791000183"); // "03.312.791/0001-83"
```

---

### 6. Configuração Personalizada (`CnpjClientOptions`)

```csharp
var options = new CnpjClientOptions
{
    MaxRequestsPerMinute = 5,
    Timeout = TimeSpan.FromSeconds(20),
    EnableCache = true,
    CacheTtl = TimeSpan.FromHours(24),
    UserAgent = "MinhaEmpresa-App/2.0",
    EnableCNPJWS = true,
    EnableReceitaWS = true,
    EnableBrasilAPI = true,
    EnableCNPJA = false // Desabilita provedor se desejar
};

using var client = new CnpjClient(options);
```

---

## 📊 Modelo de Dados Padronizado (`CnpjData`)

```csharp
public class CnpjData
{
    public string Cnpj { get; set; }                    // CNPJ formatado (XX.XXX.XXX/XXXX-XX)
    public string RazaoSocial { get; set; }             // Razão Social
    public string NomeFantasia { get; set; }            // Nome Fantasia
    public DateTime? DataAbertura { get; set; }         // Data de Abertura
    public string Situacao { get; set; }                // Situação Cadastral
    public DateTime? DataSituacao { get; set; }         // Data da Situação
    public string Tipo { get; set; }                    // Matriz / Filial
    public string Porte { get; set; }                   // Porte da Empresa
    public string NaturezaJuridica { get; set; }        // Natureza Jurídica
    public decimal? CapitalSocial { get; set; }         // Capital Social
    public Endereco Endereco { get; set; }              // Endereço Completo
    public AtividadeEconomica AtividadePrincipal { get; set; }
    public List<AtividadeEconomica> AtividadesSecundarias { get; set; }
    public List<Socio> QuadroSocietario { get; set; }   // QSA
    public List<string> Telefones { get; set; }         // Telefones
    public string Email { get; set; }                   // Email
    public List<InscricaoEstadual> InscricoesEstaduais { get; set; }  // Retornado via CNPJ.WS
    public SimplesNacional Simples { get; set; }        // Optante Simples / MEI
    public DateTime? UltimaAtualizacao { get; set; }    // Data da Atualização
    public string Provedor { get; set; }                // Provedor que respondeu
}
```

---

## 🛠️ Arquitetura do Projeto

```
GetCNPJ/
├── Cache/               # Implementação de Cache em Memória (MemoryCnpjCache)
├── Enums/               # Enumerações (ProviderType)
├── Exceptions/          # Exceções especializadas
├── Extensions/          # Extensões para DI (ServiceCollectionExtensions)
├── Interfaces/          # Contratos (ICnpjService, ICnpjProvider, ICnpjCache, IRateLimiter)
├── Models/              # DTOs padronizados e normalizados
├── Providers/           # Implementações de provedores (CNPJWS, ReceitaWS, BrasilAPI, CNPJA)
├── RateLimiter/         # SlidingWindowRateLimiter (thread-safe)
├── Services/            # CnpjService (Chain of Responsibility)
├── Utils/               # CnpjValidator (Zero-allocation validation)
├── CnpjClient.cs        # Cliente principal
├── samples/             # Projeto de console de exemplo
└── tests/               # Suíte completa de testes unitários (xUnit)
```

---

## 📄 Licença

Este projeto está licenciado sob a licença **MIT** - consulte o arquivo [LICENSE](LICENSE) para mais detalhes.

