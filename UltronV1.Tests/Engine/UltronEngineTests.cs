using System;
using UltronV1.Core.Domain;
using UltronV1.Core.Engine;
using UltronV1.Core.Features;
using UltronV1.Core.Mnq;
using UltronV1.Core.OrderFlow;
using UltronV1.Core.Signals;
using UltronV1.Tests.Support;
using Xunit;

namespace UltronV1.Tests.Engine
{
    public class UltronEngineTests
    {
        [Fact]
        public void Initialize_ValidContract_EnablesProcessing()
        {
            var logger = new ListUltronLogger();
            var engine = new UltronEngine(null, logger);

            engine.Initialize(MnqContractSpec.Symbol);

            Assert.True(engine.IsContractValid);
            Assert.Empty(logger.ErrorMessages);
        }

        [Fact]
        public void Initialize_InvalidContract_BlocksProcessing()
        {
            var logger = new ListUltronLogger();
            var engine = new UltronEngine(null, logger);

            engine.Initialize("ES 09-26");

            Assert.False(engine.IsContractValid);
            Assert.NotEmpty(logger.ErrorMessages);
        }

        [Fact]
        public void ProcessBarClose_BuildsSnapshotWithFeatures()
        {
            var logger = new ListUltronLogger();
            var engine = new UltronEngine(null, logger);
            engine.Initialize(MnqContractSpec.Symbol);

            int tickIndex = MnqContractSpec.PriceToTickIndex(20000.00);
            engine.ProcessTick(new TickEvent(
                DateTime.UtcNow.Ticks, 20000.00, 5, 19999.75, 20000.00, tickIndex));

            var indicators = MakeIndicators(20000.00, 19995.00, 25, 55, 5.0);
            var result = engine.ProcessBarClose(
                1,
                new DateTime(2026, 6, 25, 10, 0, 0),
                19999.75, 20000.25, 19999.50, 20000.00,
                100,
                indicators);

            Assert.True(result.ContractValid);
            Assert.NotNull(result.Snapshot);
            Assert.NotNull(result.Snapshot.Features);
            Assert.Equal(5, result.Snapshot.Features[FeatureSchema.BuyVolume]);
            Assert.Contains("[UltronV1] Bar=1", logger.InfoMessages[logger.InfoMessages.Count - 1]);
        }

        static IndicatorSnapshot MakeIndicators(double emaFast, double emaSlow, double adx, double rsi, double atr)
        {
            return new IndicatorSnapshot(
                emaFast, emaSlow, adx, adx - 3, rsi, atr,
                emaFast, 20,
                emaFast + 10, emaSlow - 10,
                emaFast + 8, emaSlow - 8,
                80, emaFast, emaFast - 0.5,
                true);
        }
    }
}
