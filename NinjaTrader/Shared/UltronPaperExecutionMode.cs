namespace NinjaTrader.NinjaScript.Ultron
{
    /// <summary>Visual broker execution mode for UltronV1Paper (PaperBarSim remains authoritative).</summary>
    public enum UltronPaperExecutionMode
    {
        /// <summary>PaperBarSim only — no broker orders.</summary>
        PaperBarSim,

        /// <summary>Managed strategy EnterLong/SetStopLoss/SetProfitTarget (legacy visual).</summary>
        Sim101Visual,

        /// <summary>NinjaTrader ATM template via account API (visual only).</summary>
        NinjaTraderAtm
    }
}
