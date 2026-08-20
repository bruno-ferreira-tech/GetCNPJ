using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GetCNPJ.Enums;
using GetCNPJ.Extensions;
using GetCNPJ.Interfaces;
using GetCNPJ.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

// Configuração do GetCNPJ com Injeção de Dependência e Cache
builder.Services.AddGetCNPJ(options =>
{
    options.MaxRequestsPerMinute = 3;
    options.EnableCache = true;
    options.CacheTtl = TimeSpan.FromHours(12);
    options.EnableCNPJWS = true;
    options.EnableReceitaWS = true;
    options.EnableBrasilAPI = true;
    options.EnableCNPJA = true;
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "GetCNPJ API",
        Version = "v1",
        Description = "API REST de alta performance para consulta de CNPJs com múltiplos provedores, fallback automático e cache em memória."
    });
});

var app = builder.Build();

// Swagger sempre habilitado para facilitar testes no Dokploy
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "GetCNPJ API v1");
    c.RoutePrefix = string.Empty; // Abre o Swagger na raiz "/"
});

// Health Check
app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    timestamp = DateTime.UtcNow,
    version = "1.1.0"
}))
.WithName("HealthCheck")
.WithTags("Monitoramento");

// Consulta Simples de CNPJ (com fallback automático e cache)
app.MapGet("/api/cnpj/{cnpj}", async (string cnpj, ICnpjService cnpjService, CancellationToken ct) =>
{
    var result = await cnpjService.GetCnpjAsync(cnpj, ct);

    if (result.Success && result.Data != null)
    {
        return Results.Ok(result.Data);
    }

    return Results.NotFound(new
    {
        error = result.ErrorMessage,
        details = result.Errors
    });
})
.WithName("GetCnpj")
.WithTags("Consulta de CNPJ")
.Produces<CnpjData>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status404NotFound);

// Consulta de CNPJ por Provedor Específico (CNPJWS, ReceitaWS, BrasilAPI, CNPJA)
app.MapGet("/api/cnpj/{cnpj}/provider/{provider}", async (string cnpj, ProviderType provider, ICnpjService cnpjService, CancellationToken ct) =>
{
    var result = await cnpjService.GetCnpjFromProviderAsync(cnpj, provider, ct);

    if (result.Success && result.Data != null)
    {
        return Results.Ok(result.Data);
    }

    return Results.NotFound(new
    {
        error = result.ErrorMessage,
        details = result.Errors
    });
})
.WithName("GetCnpjFromProvider")
.WithTags("Consulta de CNPJ")
.Produces<CnpjData>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status404NotFound);

// Consulta em Lote (Batch)
app.MapPost("/api/cnpj/batch", async (List<string> cnpjs, ICnpjService cnpjService, CancellationToken ct) =>
{
    if (cnpjs == null || cnpjs.Count == 0)
    {
        return Results.BadRequest(new { error = "A lista de CNPJs não pode ser vazia." });
    }

    var results = await cnpjService.GetBatchAsync(cnpjs, maxDegreeOfParallelism: 2, cancellationToken: ct);
    return Results.Ok(results);
})
.WithName("GetCnpjBatch")
.WithTags("Consulta de CNPJ")
.Produces<IReadOnlyList<CnpjResult>>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status400BadRequest);

app.Run();
