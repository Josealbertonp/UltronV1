#region Using declarations
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.IO;
using NinjaTrader.Cbi;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using UltronV1.Core.Dataset;
using UltronV1.Core.Domain;
using UltronV1.Core.Engine;
using UltronV1.Core.Execution;
using UltronV1.Core.Mnq;
using UltronV1.Core.OrderFlow;
using UltronV1.Core.Signals;
#endregion

namespace NinjaTrader.NinjaScript.Strategies
{
    /// <summary>
    /// Phase 5 — Outcome Collection &amp; Paper Execution.
    /// Consumes Ultron signals only (no new entry rules). Writes outcomes.jsonl for ML/Validation.
    /// Use on Replay / Sim101 / Paper — not for live capital.
    /// </summary>
    public class UltronV1Paper : Strategy
    {
        UltronEngine _engine;
        DatasetEngine _dataset;
        TradeCycleTracker _tradeCycle;
        NtPrintLogger _logger;

        EMA _emaFast;
        EMA _emaSlow;
        EMA _emaMtf;
        ADX _adx;
        ATR _atr;
        RSI _rsi;
        DonchianChannel _donchian;
        SMA _volSma;

        double _vwapNumerator;
        double _vwapDenominator;
        int _vwapBarCount;
        bool _vwapResetToday;

        int _outcomesRecorded;

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = "Ultron V1 Paper — outcome collection for ML/validation (Replay/Sim101/Paper only)";
                Name = "UltronV1Paper";
                Calculate = Calculate.OnEachTick;
                EntriesPerDirection = 1;
                EntryHandling = EntryHandling.AllEntries;
                IsExitOnSessionCloseStrategy = true;
                ExitOnSessionCloseSeconds = 30;
                IsFillLimitOnTouch = false;
                MaximumBarsLookBack = MaximumBarsLookBack.TwoHundredFiftySix;
                OrderFillResolution = OrderFillResolution.Standard;
                Slippage = 1;
                StartBehavior = StartBehavior.WaitUntilFlat;
                TimeInForce = TimeInForce.Gtc;
                TraceOrders = false;
                RealtimeErrorHandling = RealtimeErrorHandling.StopCancelClose;
                StopTargetHandling = StopTargetHandling.PerEntryExecution;
                BarsRequiredToTrade = 25;
                IsInstantiatedOnEachOptimizationIteration = true;

                PaperStopLossTicks = 30;
                PaperTakeProfitTicks = 32;
                PaperMaxBarsInTrade = 48;
                MinTierForEntry = SignalTier.B;
                SubmitSimOrders = true;
            }
            else if (State == State.Configure)
            {
                AddDataSeries(BarsPeriodType.Minute, MnqContractSpec.MtfBarPeriodMinutes);
            }
            else if (State == State.DataLoaded)
            {
                _logger = new NtPrintLogger(this);

                var cfg = new UltronConfig
                {
                    EnableDiagnosticsLog = true,
                    EnableExplainLog = false,
                    EnableDatasetEngine = true,
                    EnableOutcomeCollection = true,
                    MinSignalTier = MinTierForEntry,
                    PaperStopLossTicks = PaperStopLossTicks,
                    PaperTakeProfitTicks = PaperTakeProfitTicks,
                    PaperMaxBarsInTrade = PaperMaxBarsInTrade,
                    ConfigVersion = UltronVersions.DefaultConfigVersion
                };

                string root = Path.Combine(Globals.UserDataDir, "UltronV1", "dataset");
                _dataset = new DatasetEngine(new JsonLinesDatasetSink(root, _logger), _logger);

                _tradeCycle = new TradeCycleTracker(new PaperExecutionConfig
                {
                    StopLossTicks = PaperStopLossTicks,
                    TakeProfitTicks = PaperTakeProfitTicks,
                    MaxBarsInTrade = PaperMaxBarsInTrade,
                    ExecutionMode = "PaperBarSim"
                });

                _engine = new UltronEngine(cfg, _logger, _dataset);
                _engine.Initialize(Instrument.FullName);

                if (!_engine.IsContractValid)
                    Print("[UltronV1Paper] INVALID CONTRACT — MNQ 09-26 required.");

                _emaFast = EMA(Close, 9);
                _emaSlow = EMA(Close, 21);
                _adx = ADX(Close, 14);
                _atr = ATR(Close, 14);
                _rsi = RSI(Close, 14, 3);
                _donchian = DonchianChannel(Close, 20);
                _volSma = SMA(Volume, 20);
                _emaMtf = EMA(Closes[1], 21);

                Print(string.Format("[UltronV1Paper] Outcome collection started. Session={0}", _dataset.SessionId));
            }
            else if (State == State.Terminated)
            {
                Print(string.Format("[UltronV1Paper] Session ended. Outcomes recorded={0}", _outcomesRecorded));
                _engine?.Shutdown();
                _engine = null;
                _dataset = null;
                _tradeCycle = null;
            }
        }

        protected override void OnMarketData(MarketDataEventArgs e)
        {
            if (_engine == null || !_engine.IsContractValid)
                return;
            if (e.MarketDataType != MarketDataType.Last)
                return;

            _engine.ProcessTick(new TickEvent(
                e.Time.ToUniversalTime().Ticks,
                e.Price,
                e.Volume,
                GetCurrentBid(),
                GetCurrentAsk(),
                MnqContractSpec.PriceToTickIndex(e.Price)));
        }

        protected override void OnBarUpdate()
        {
            if (_engine == null || !_engine.IsContractValid)
                return;
            if (BarsInProgress != 0)
                return;
            if (CurrentBar < BarsRequiredToTrade)
                return;

            UpdateVwap();

            var result = _engine.ProcessBarClose(
                CurrentBar,
                Time[0],
                Open[0],
                High[0],
                Low[0],
                Close[0],
                (long)Volume[0],
                BuildIndicatorSnapshot());

            ProcessOutcomeCycle(result);

            if (result.Signal != null && result.Signal.Tier >= MinTierForEntry && !_tradeCycle.IsOpen)
                TryOpenPaperTrade(result);
        }

        void ProcessOutcomeCycle(BarEvaluationResult result)
        {
            if (!_tradeCycle.IsOpen)
                return;

            var outcome = _tradeCycle.OnBar(
                High[0], Low[0], Close[0],
                Time[0].ToUniversalTime(),
                CurrentBar);

            if (outcome == null)
                return;

            if (_dataset.EnrichOutcome(outcome))
            {
                _outcomesRecorded++;
                Print(string.Format(
                    "[UltronV1Paper] OUTCOME {0} id={1} R={2:F2} PnL={3:F1}t reason={4}",
                    outcome.IsWin ? "WIN" : "LOSS",
                    outcome.SignalId,
                    outcome.RMultiple,
                    outcome.RealizedPnLTicks,
                    outcome.ExitReason));
            }

            if (SubmitSimOrders && Position.MarketPosition != MarketPosition.Flat)
            {
                if (Position.MarketPosition == MarketPosition.Long)
                    ExitLong("UV1_Exit", "UV1_L");
                else if (Position.MarketPosition == MarketPosition.Short)
                    ExitShort("UV1_Exit", "UV1_S");
            }
        }

        void TryOpenPaperTrade(BarEvaluationResult result)
        {
            if (result.Signal == null || string.IsNullOrEmpty(result.LastSignalId))
                return;

            double entry = Close[0];
            if (!_tradeCycle.TryOpen(
                    result.LastSignalId,
                    result.Signal.Direction,
                    entry,
                    Time[0].ToUniversalTime(),
                    CurrentBar))
                return;

            Print(string.Format(
                "[UltronV1Paper] OPEN {0} id={1} score={2} tier={3} entry={4:F2}",
                result.Signal.Direction,
                result.LastSignalId,
                result.Signal.Score,
                result.Signal.Tier,
                entry));

            if (!SubmitSimOrders)
                return;

            if (result.Signal.Direction == TradeDirection.Long)
            {
                EnterLong(1, "UV1_L");
                SetStopLoss(CalculationMode.Ticks, PaperStopLossTicks);
                SetProfitTarget(CalculationMode.Ticks, PaperTakeProfitTicks);
            }
            else
            {
                EnterShort(1, "UV1_S");
                SetStopLoss(CalculationMode.Ticks, PaperStopLossTicks);
                SetProfitTarget(CalculationMode.Ticks, PaperTakeProfitTicks);
            }
        }

        IndicatorSnapshot BuildIndicatorSnapshot()
        {
            int accelBars = _engine.Config.AccelLookbackBars;
            double adxPrev = _adx.IsValidDataPoint(accelBars) ? _adx[accelBars] : _adx[0];

            return new IndicatorSnapshot(
                _emaFast[0], _emaSlow[0], _adx[0], adxPrev, _rsi[0], _atr[0],
                _vwapDenominator > 0 ? _vwapNumerator / _vwapDenominator : Close[0],
                _vwapBarCount,
                _donchian.Upper[0], _donchian.Lower[0],
                _donchian.IsValidDataPoint(1) ? _donchian.Upper[1] : _donchian.Upper[0],
                _donchian.IsValidDataPoint(1) ? _donchian.Lower[1] : _donchian.Lower[0],
                _volSma.IsValidDataPoint(0) ? _volSma[0] : Volume[0],
                _emaMtf.IsValidDataPoint(0) ? _emaMtf[0] : _emaFast[0],
                _emaMtf.IsValidDataPoint(2) ? _emaMtf[2] : _emaMtf[0],
                true);
        }

        void UpdateVwap()
        {
            if (Bars.IsFirstBarOfSession)
            {
                _vwapNumerator = 0;
                _vwapDenominator = 0;
                _vwapBarCount = 0;
                _vwapResetToday = true;
            }

            double typical = (High[0] + Low[0] + Close[0]) / 3.0;
            _vwapNumerator += typical * Volume[0];
            _vwapDenominator += Volume[0];
            _vwapBarCount++;
        }

        #region Properties

        [NinjaScriptProperty]
        [Range(8, 100)]
        [Display(Name = "Paper Stop (ticks)", Order = 1, GroupName = "Ultron Paper")]
        public int PaperStopLossTicks { get; set; }

        [NinjaScriptProperty]
        [Range(8, 200)]
        [Display(Name = "Paper Target (ticks)", Order = 2, GroupName = "Ultron Paper")]
        public int PaperTakeProfitTicks { get; set; }

        [NinjaScriptProperty]
        [Range(4, 200)]
        [Display(Name = "Max Bars In Trade", Order = 3, GroupName = "Ultron Paper")]
        public int PaperMaxBarsInTrade { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Min Tier For Entry", Order = 4, GroupName = "Ultron Paper")]
        public SignalTier MinTierForEntry { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Submit Sim101 Orders", Order = 5, GroupName = "Ultron Paper")]
        public bool SubmitSimOrders { get; set; }

        #endregion

        sealed class NtPrintLogger : IUltronLogger
        {
            readonly Strategy _host;
            public NtPrintLogger(Strategy host) { _host = host; }
            public void Info(string message) { _host.Print(message); }
            public void Warn(string message) { _host.Print(message); }
            public void Error(string message) { _host.Print(message); }
        }
    }
}
