using NinjaTrader.Cbi;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using global::UltronV1.Core.Engine;
using global::UltronV1.Core.Mnq;
using global::UltronV1.Core.OrderFlow;

namespace NinjaTrader.NinjaScript.Ultron
{
    /// <summary>Shared last-tick feed into UltronEngine.</summary>
    public static class UltronTickFeed
    {
        public static void ProcessLastTick(
            UltronEngine engine,
            MarketDataEventArgs e,
            GetBidAskDelegate getBidAsk)
        {
            if (engine == null || !engine.IsContractValid)
                return;
            if (e.MarketDataType != MarketDataType.Last)
                return;

            getBidAsk(out double bid, out double ask);

            engine.ProcessTick(new TickEvent(
                e.Time.ToUniversalTime().Ticks,
                e.Price,
                e.Volume,
                bid,
                ask,
                MnqContractSpec.PriceToTickIndex(e.Price)));
        }

        public delegate void GetBidAskDelegate(out double bid, out double ask);
    }
}
