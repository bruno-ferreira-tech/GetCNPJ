using GetCNPJ.Extensions;
using GetCNPJ.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GetCNPJ.Tests
{
    public class DependencyInjectionTests
    {
        [Fact]
        public void AddGetCNPJ_ShouldRegisterRequiredServices()
        {
            var services = new ServiceCollection();
            services.AddGetCNPJ(options =>
            {
                options.EnableCache = true;
                options.EnableCNPJWS = true;
            });

            var provider = services.BuildServiceProvider();

            var cnpjService = provider.GetService<ICnpjService>();
            Assert.NotNull(cnpjService);

            var cnpjClient = provider.GetService<CnpjClient>();
            Assert.NotNull(cnpjClient);

            var rateLimiter = provider.GetService<IRateLimiter>();
            Assert.NotNull(rateLimiter);

            var cache = provider.GetService<ICnpjCache>();
            Assert.NotNull(cache);
        }
    }
}
