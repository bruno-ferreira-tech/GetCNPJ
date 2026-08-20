using GetCNPJ.Utils;
using Xunit;

namespace GetCNPJ.Tests
{
    public class CnpjValidatorTests
    {
        [Theory]
        [InlineData("03.312.791/0001-83", true)]
        [InlineData("03312791000183", true)]
        [InlineData("00.000.000/0001-91", true)]
        [InlineData("00000000000191", true)]
        [InlineData("18.236.120/0001-58", true)]
        [InlineData("18236120000158", true)]
        [InlineData("33.000.167/0001-01", true)]
        public void IsValid_ShouldReturnTrue_ForValidCnpj(string cnpj, bool expected)
        {
            var result = CnpjValidator.IsValid(cnpj);
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("00.000.000/0000-00")]
        [InlineData("11.111.111/1111-11")]
        [InlineData("22222222222222")]
        [InlineData("03.312.791/0001-84")] // Dígito inválido
        [InlineData("12345678901234")]
        [InlineData("12345")]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public void IsValid_ShouldReturnFalse_ForInvalidCnpj(string? cnpj)
        {
            var result = CnpjValidator.IsValid(cnpj);
            Assert.False(result);
        }

        [Theory]
        [InlineData("03.312.791/0001-83", "03312791000183")]
        [InlineData("03312791000183", "03312791000183")]
        [InlineData(" 03-312-791/0001.83 ", "03312791000183")]
        [InlineData("", "")]
        [InlineData(null, "")]
        public void Normalize_ShouldStripNonNumericCharacters(string? input, string expected)
        {
            var result = CnpjValidator.Normalize(input);
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("03312791000183", "03.312.791/0001-83")]
        [InlineData("03.312.791/0001-83", "03.312.791/0001-83")]
        public void Format_ShouldFormatCnpjProperly(string input, string expected)
        {
            var result = CnpjValidator.Format(input);
            Assert.Equal(expected, result);
        }
    }
}
