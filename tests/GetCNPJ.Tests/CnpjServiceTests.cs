using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GetCNPJ.Cache;
using GetCNPJ.Interfaces;
using GetCNPJ.Models;
using GetCNPJ.Services;
using Xunit;

namespace GetCNPJ.Tests
{
    public class CnpjServiceTests
    {
        private class MockProvider : ICnpjProvider
        {
            public string ProviderName { get; }
            public int Priority { get; }
            private readonly Func<string, Task<CnpjData?>> _behavior;

            public MockProvider(string name, int priority, Func<string, Task<CnpjData?>> behavior)
            {
                ProviderName = name;
                Priority = priority;
                _behavior = behavior;
            }

            public Task<CnpjData?> GetCnpjDataAsync(string cnpj, CancellationToken cancellationToken = default)
            {
                return _behavior(cnpj);
            }

            public Task<bool> IsAvailableAsync() => Task.FromResult(true);
        }

        [Fact]
        public async Task GetCnpjAsync_ShouldFallbackToSecondProvider_WhenFirstFails()
        {
            var p1 = new MockProvider("P1", 1, _ => throw new Exception("Falha no P1"));
            var p2 = new MockProvider("P2", 2, cnpj => Task.FromResult<CnpjData?>(new CnpjData
            {
                Cnpj = cnpj,
                RazaoSocial = "Empresa Fallback"
            }));

            var service = new CnpjService(new[] { p1, p2 });
            var result = await service.GetCnpjAsync("03312791000183");

            Assert.True(result.Success);
            Assert.NotNull(result.Data);
            Assert.Equal("Empresa Fallback", result.Data.RazaoSocial);
        }

        [Fact]
        public async Task GetCnpjAsync_ShouldReturnError_WhenAllProvidersFail()
        {
            var p1 = new MockProvider("P1", 1, _ => throw new Exception("Erro P1"));
            var p2 = new MockProvider("P2", 2, _ => Task.FromResult<CnpjData?>(null));

            var service = new CnpjService(new[] { p1, p2 });
            var result = await service.GetCnpjAsync("03312791000183");

            Assert.False(result.Success);
            Assert.Equal(2, result.Errors.Count);
        }

        [Fact]
        public async Task GetCnpjAsync_ShouldReturnError_WhenCnpjIsInvalid()
        {
            var p1 = new MockProvider("P1", 1, cnpj => Task.FromResult<CnpjData?>(new CnpjData()));
            var service = new CnpjService(new[] { p1 });

            var result = await service.GetCnpjAsync("11111111111111");

            Assert.False(result.Success);
            Assert.Contains("inválido", result.ErrorMessage);
        }

        [Fact]
        public async Task GetCnpjAsync_ShouldUseCache_WhenAvailable()
        {
            using var cache = new MemoryCnpjCache();
            var cachedData = new CnpjData
            {
                Cnpj = "03.312.791/0001-83",
                RazaoSocial = "Empresa em Cache"
            };
            await cache.SetAsync("03312791000183", cachedData);

            int providerCallCount = 0;
            var p1 = new MockProvider("P1", 1, _ =>
            {
                providerCallCount++;
                return Task.FromResult<CnpjData?>(new CnpjData { RazaoSocial = "API Real" });
            });

            var service = new CnpjService(new[] { p1 }, cache);
            var result = await service.GetCnpjAsync("03312791000183");

            Assert.True(result.Success);
            Assert.Equal("Empresa em Cache", result.Data?.RazaoSocial);
            Assert.Equal(0, providerCallCount); // Provedor não deve ser chamado
        }

        [Fact]
        public async Task GetBatchAsync_ShouldProcessAllCnpjsInOrder()
        {
            var p1 = new MockProvider("P1", 1, cnpj => Task.FromResult<CnpjData?>(new CnpjData
            {
                Cnpj = cnpj,
                RazaoSocial = $"Empresa {cnpj}"
            }));

            var service = new CnpjService(new[] { p1 });
            var cnpjs = new[] { "03312791000183", "00000000000191" };

            var results = await service.GetBatchAsync(cnpjs);

            Assert.Equal(2, results.Count);
            Assert.True(results[0].Success);
            Assert.True(results[1].Success);
            Assert.Equal("Empresa 03312791000183", results[0].Data?.RazaoSocial);
            Assert.Equal("Empresa 00000000000191", results[1].Data?.RazaoSocial);
        }
    }
}
