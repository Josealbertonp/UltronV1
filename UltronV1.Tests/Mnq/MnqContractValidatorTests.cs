using UltronV1.Core.Mnq;
using Xunit;

namespace UltronV1.Tests.Mnq
{
    public class MnqContractValidatorTests
    {
        [Theory]
        [InlineData("MNQ 09-26", true)]
        [InlineData("mnq 09-26", true)]
        [InlineData("MNQ SEP26", true)]
        public void Validate_AcceptsMnq0926(string instrument, bool expectedValid)
        {
            var result = MnqContractValidator.Validate(instrument);
            Assert.Equal(expectedValid, result.IsValid);
        }

        [Theory]
        [InlineData("ES 09-26")]
        [InlineData("MNQ 12-26")]
        [InlineData("")]
        public void Validate_RejectsInvalid(string instrument)
        {
            var result = MnqContractValidator.Validate(instrument);
            Assert.False(result.IsValid);
        }

        [Fact]
        public void PriceToTickIndex_UsesQuarterPointGrid()
        {
            Assert.Equal(80000, MnqContractSpec.PriceToTickIndex(20000.0));
            Assert.Equal(80001, MnqContractSpec.PriceToTickIndex(20000.25));
        }
    }
}
