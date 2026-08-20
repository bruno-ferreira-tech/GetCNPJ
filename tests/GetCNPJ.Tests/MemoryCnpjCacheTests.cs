using System;
using System.Threading.Tasks;
using GetCNPJ.Cache;
using GetCNPJ.Models;
using Xunit;

namespace GetCNPJ.Tests
{
    public class MemoryCnpjCacheTests
    {
        [Fact]
        public async Task Cache_ShouldStoreAndRetrieveData()
        {
            using var cache = new MemoryCnpjCache(TimeSpan.FromMinutes(5));
            var cnpj = "03312791000183";
            var data = new CnpjData
            {
                Cnpj = "03.312.791/0001-83",
                RazaoSocial = "Empresa Teste LTDA"
            };

            await cache.SetAsync(cnpj, data);

            var retrieved = await cache.GetAsync(cnpj);
            Assert.NotNull(retrieved);
            Assert.Equal("Empresa Teste LTDA", retrieved.RazaoSocial);
        }

        [Fact]
        public async Task Cache_ShouldNormalizeKey_ForFormattedAndCleanCnpj()
        {
            using var cache = new MemoryCnpjCache(TimeSpan.FromMinutes(5));
            var formattedCnpj = "03.312.791/0001-83";
            var cleanCnpj = "03312791000183";

            var data = new CnpjData
            {
                Cnpj = formattedCnpj,
                RazaoSocial = "Empresa Teste"
            };

            // Salva com formato com máscara
            await cache.SetAsync(formattedCnpj, data);

            // Recupera apenas com os dígitos
            var retrieved = await cache.GetAsync(cleanCnpj);
            Assert.NotNull(retrieved);
            Assert.Equal("Empresa Teste", retrieved.RazaoSocial);
        }

        [Fact]
        public async Task RemoveAsync_ShouldRemoveEntry()
        {
            using var cache = new MemoryCnpjCache(TimeSpan.FromMinutes(5));
            var cnpj = "03312791000183";
            var data = new CnpjData { Cnpj = cnpj, RazaoSocial = "Empresa Teste" };

            await cache.SetAsync(cnpj, data);
            await cache.RemoveAsync(cnpj);

            var retrieved = await cache.GetAsync(cnpj);
            Assert.Null(retrieved);
        }
    }
}
