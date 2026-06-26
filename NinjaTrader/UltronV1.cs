#region Using declarations
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.IO;
using System.Windows.Media;
using NinjaTrader.Cbi;
using NinjaTrader.Data;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Tools;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.DrawingTools;
using NinjaTrader.NinjaScript.Indicators;
using UltronV1.Core.Domain;
using UltronV1.Core.Dataset;
using UltronV1.Core.Engine;
using UltronV1.Core.Mnq;
using UltronV1.Core.OrderFlow;
using UltronV1.Core.Signals;
#endregion

namespace NinjaTrader.NinjaScript.Indicators
{
    public class UltronV1 : Indicator
    {
        UltronEngine _engine;
        NtPrintLogger _logger;
        UltronComparisonTracker _comparison;

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

        string _hudTag = "UltronV1_HUD";

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = "Ultron V1 — MNQ 09-26 order-flow + confluence + audit";
                Name = "UltronV1";
                Calculate = Calculate.OnEachTick;
                IsOverlay = true;
                DisplayInDataBox = true;
                DrawOnPricePanel = true;
                IsSuspendedWhileInactive = true;

                EnableDiagnostics = true;
                EnableExplainMode = true;
                ShowSignalArrows = true;
                ShowHud = true;
                EnableComparisonMode = false;
                EnableDatasetEngine = false;
                MinTierForArrow = SignalTier.B;
            }
            else if (State == State.Configure)
            {
                AddDataSeries(BarsPeriodType.Minute, MnqContractSpec.MtfBarPeriodMinutes);
            }
            else if (State == State.DataLoaded)
            {
                _logger = new NtPrintLogger(this);
                _comparison = new UltronComparisonTracker();

                var cfg = new UltronConfig
                {
                    EnableDiagnosticsLog = EnableDiagnostics,
                    EnableExplainLog = EnableExplainMode,
                    EnableDatasetEngine = EnableDatasetEngine,
                    MinSignalTier = MinTierForArrow,
                    ConfigVersion = UltronVersions.DefaultConfigVersion
                };

                DatasetEngine? dataset = null;
                if (EnableDatasetEngine)
                {
                    string root = Path.Combine(NinjaTrader.Core.Globals.UserDataDir, "UltronV1", "dataset");
                    dataset = new DatasetEngine(new JsonLinesDatasetSink(root, _logger), _logger);
                }

                _engine = new UltronEngine(cfg, _logger, dataset);
                _engine.Initialize(Instrument.FullName);

                if (!_engine.IsContractValid)
                    BackBrush = Brushes.DarkRed;

                _emaFast = EMA(Close, 9);
                _emaSlow = EMA(Close, 21);
                _adx = ADX(Close, 14);
                _atr = ATR(Close, 14);
                _rsi = RSI(Close, 14, 3);
                _donchian = DonchianChannel(Close, 20);
                _volSma = SMA(Volume, 20);
                _emaMtf = EMA(Closes[1], 21);

                if (EnableComparisonMode)
                {
                    string logDir = Path.Combine(
                        NinjaTrader.Core.Globals.UserDataDir,
                        "UltronV1");
                    _comparison.OpenLog(logDir);
                }
            }
            else if (State == State.Terminated)
            {
                _engine?.Shutdown();
                _comparison?.CloseLog();
                _engine = null;
                _logger = null;
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
            if (CurrentBar < 25)
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

            TradeDirection ultronDir = result.Signal?.Direction ?? TradeDirection.None;
            int ultronScore = result.Signal?.Score ?? 0;
            SignalTier tier = result.Signal?.Tier ?? result.Explanation.Tier;

            if (EnableComparisonMode)
            {
                TradeDirection nrDir = _comparison.DetectNightRangerOnBar(this, CurrentBar);
                _comparison.RecordBar(Time[0], CurrentBar, ultronDir, ultronScore, tier, nrDir);
                if (nrDir != TradeDirection.None && ultronDir == TradeDirection.None)
                    DrawComparisonMarker(CurrentBar, nrDir, false);
                else if (ultronDir != TradeDirection.None && nrDir == TradeDirection.None)
                    DrawComparisonMarker(CurrentBar, ultronDir, true);
                else if (ultronDir != TradeDirection.None && nrDir != TradeDirection.None && ultronDir == nrDir)
                    DrawComparisonMatch(CurrentBar);
            }

            if (result.Signal != null && ShowSignalArrows && result.Signal.Tier >= MinTierForArrow)
                DrawSignal(result);

            if (ShowHud)
                DrawHud(result);
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
            int timeEt = ToTime(Times[0][0]);
            if (timeEt >= 93000 && !_vwapResetToday)
            {
                _vwapNumerator = 0;
                _vwapDenominator = 0;
                _vwapBarCount = 0;
                _vwapResetToday = true;
            }
            if (timeEt < 93000)
                _vwapResetToday = false;

            double typical = (High[0] + Low[0] + Close[0]) / 3.0;
            double vol = Volume[0] > 0 ? Volume[0] : 1;
            _vwapNumerator += typical * vol;
            _vwapDenominator += vol;
            _vwapBarCount++;
        }

        void DrawSignal(BarEvaluationResult result)
        {
            var sig = result.Signal!;
            string tag = "UltronV1_Sig_" + CurrentBar;
            Brush color = sig.Tier == SignalTier.A ? Brushes.LimeGreen
                : sig.Tier == SignalTier.B ? Brushes.DodgerBlue
                : Brushes.Goldenrod;

            if (sig.Direction == TradeDirection.Long)
            {
                Draw.ArrowUp(this, tag, true, 0, Low[0] - 4 * TickSize, color);
                Draw.Text(this, tag + "_t", sig.Reason, 0, Low[0] - 8 * TickSize, color);
            }
            else
            {
                Draw.ArrowDown(this, tag, true, 0, High[0] + 4 * TickSize, color);
                Draw.Text(this, tag + "_t", sig.Reason, 0, High[0] + 8 * TickSize, color);
            }

            if (EnableExplainMode)
            {
                string explain = result.Explanation.FormatScoreLines(sig.Direction);
                Draw.Text(this, tag + "_x", explain, 0,
                    sig.Direction == TradeDirection.Long ? Low[0] - 14 * TickSize : High[0] + 14 * TickSize,
                    Brushes.LightGray);
            }
        }

        void DrawComparisonMarker(int bar, TradeDirection dir, bool ultronOnly)
        {
            string tag = "UltronV1_Cmp_" + bar;
            double y = dir == TradeDirection.Long ? Low[0] - 2 * TickSize : High[0] + 2 * TickSize;
            Draw.Dot(this, tag, true, 0, y, ultronOnly ? Brushes.Cyan : Brushes.DimGray);
        }

        void DrawComparisonMatch(int bar)
        {
            Draw.Dot(this, "UltronV1_Match_" + bar, true, 0, Close[0], Brushes.MediumPurple);
        }

        void DrawHud(BarEvaluationResult result)
        {
            var snap = result.Snapshot;
            var of = snap.OrderFlow;
            var ex = result.Explanation;
            int displayScore = Math.Max(result.Score.LongScore, result.Score.ShortScore);
            SignalTier displayTier = result.Signal?.Tier ?? ex.Tier;

            string trend = snap.ContinuationLong ? "UP" : snap.ContinuationShort ? "DN" : "—";
            string gates = result.Gates.LongAllowed ? "L✓" : "L✗";
            gates += result.Gates.ShortAllowed ? " S✓" : " S✗";

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("── Ultron V1 ──");
            sb.AppendFormat(CultureInfo.InvariantCulture, "Score {0}  Tier {1}\n", displayScore, displayTier);
            sb.AppendFormat(CultureInfo.InvariantCulture, "Trend {0}  Regime {1}\n", trend, ex.Regime);
            sb.AppendFormat(CultureInfo.InvariantCulture, "OF Δ{0} RVOL {1:F2} BA {2:F2}\n", of.Delta, snap.Rvol, of.BidAskPressureRatio);
            sb.AppendFormat("Gates {0}\n", gates);

            if (EnableComparisonMode && _comparison != null)
                sb.AppendLine(_comparison.FormatHudLine());

            if (EnableDatasetEngine && _engine?.Dataset != null)
                sb.AppendFormat(CultureInfo.InvariantCulture, "DS {0} rec={1} acc={2}\n",
                    _engine.Dataset.SessionId, _engine.Dataset.RecordCount, _engine.Dataset.AcceptedCount);

            Draw.TextFixed(this, _hudTag, sb.ToString().TrimEnd(),
                TextPosition.TopLeft,
                Brushes.White,
                new SimpleFont("Consolas", 11),
                Brushes.Transparent,
                Brushes.FromArgb(160, 20, 20, 30),
                100);
        }

        #region Properties

        [NinjaScriptProperty]
        [Display(Name = "Enable Diagnostics Log", Order = 1, GroupName = "Ultron V1")]
        public bool EnableDiagnostics { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Enable Explain Mode", Order = 2, GroupName = "Ultron V1")]
        public bool EnableExplainMode { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Show Signal Arrows", Order = 3, GroupName = "Ultron V1")]
        public bool ShowSignalArrows { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Show HUD", Order = 4, GroupName = "Ultron V1")]
        public bool ShowHud { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Enable NR Comparison Mode", Order = 5, GroupName = "Ultron V1")]
        public bool EnableComparisonMode { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Enable Dataset Engine", Order = 6, GroupName = "Ultron V1")]
        public bool EnableDatasetEngine { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Min Tier For Arrow", Order = 7, GroupName = "Ultron V1")]
        public SignalTier MinTierForArrow { get; set; }

        #endregion

        sealed class NtPrintLogger : IUltronLogger
        {
            readonly Indicator _host;
            public NtPrintLogger(Indicator host) { _host = host; }
            public void Info(string message) { _host.Print(message); }
            public void Warn(string message) { _host.Print(message); }
            public void Error(string message) { _host.Print(message); }
        }
    }
}

#region NinjaScript generated code
namespace NinjaTrader.NinjaScript.Indicators
{
    public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
    {
        private UltronV1[] cacheUltronV1;

        public UltronV1 UltronV1(bool enableDiagnostics, bool enableExplainMode, bool showSignalArrows, bool showHud, bool enableComparisonMode, bool enableDatasetEngine, SignalTier minTierForArrow)
        {
            return UltronV1(Input, enableDiagnostics, enableExplainMode, showSignalArrows, showHud, enableComparisonMode, enableDatasetEngine, minTierForArrow);
        }

        public UltronV1 UltronV1(ISeries<double> input, bool enableDiagnostics, bool enableExplainMode, bool showSignalArrows, bool showHud, bool enableComparisonMode, bool enableDatasetEngine, SignalTier minTierForArrow)
        {
            if (cacheUltronV1 != null)
                for (int idx = 0; idx < cacheUltronV1.Length; idx++)
                    if (cacheUltronV1[idx] != null
                        && cacheUltronV1[idx].EnableDiagnostics == enableDiagnostics
                        && cacheUltronV1[idx].EnableExplainMode == enableExplainMode
                        && cacheUltronV1[idx].ShowSignalArrows == showSignalArrows
                        && cacheUltronV1[idx].ShowHud == showHud
                        && cacheUltronV1[idx].EnableComparisonMode == enableComparisonMode
                        && cacheUltronV1[idx].EnableDatasetEngine == enableDatasetEngine
                        && cacheUltronV1[idx].MinTierForArrow == minTierForArrow
                        && cacheUltronV1[idx].EqualsInput(input))
                        return cacheUltronV1[idx];

            return CacheIndicator<UltronV1>(
                new UltronV1
                {
                    EnableDiagnostics = enableDiagnostics,
                    EnableExplainMode = enableExplainMode,
                    ShowSignalArrows = showSignalArrows,
                    ShowHud = showHud,
                    EnableComparisonMode = enableComparisonMode,
                    EnableDatasetEngine = enableDatasetEngine,
                    MinTierForArrow = minTierForArrow
                },
                input,
                ref cacheUltronV1);
        }
    }
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
    public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
    {
        public Indicators.UltronV1 UltronV1(bool enableDiagnostics, bool enableExplainMode, bool showSignalArrows, bool showHud, bool enableComparisonMode, bool enableDatasetEngine, SignalTier minTierForArrow)
        {
            return indicator.UltronV1(Input, enableDiagnostics, enableExplainMode, showSignalArrows, showHud, enableComparisonMode, enableDatasetEngine, minTierForArrow);
        }

        public Indicators.UltronV1 UltronV1(ISeries<double> input, bool enableDiagnostics, bool enableExplainMode, bool showSignalArrows, bool showHud, bool enableComparisonMode, bool enableDatasetEngine, SignalTier minTierForArrow)
        {
            return indicator.UltronV1(input, enableDiagnostics, enableExplainMode, showSignalArrows, showHud, enableComparisonMode, enableDatasetEngine, minTierForArrow);
        }
    }
}

namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
    {
        public Indicators.UltronV1 UltronV1(bool enableDiagnostics, bool enableExplainMode, bool showSignalArrows, bool showHud, bool enableComparisonMode, bool enableDatasetEngine, SignalTier minTierForArrow)
        {
            return indicator.UltronV1(Input, enableDiagnostics, enableExplainMode, showSignalArrows, showHud, enableComparisonMode, enableDatasetEngine, minTierForArrow);
        }

        public Indicators.UltronV1 UltronV1(ISeries<double> input, bool enableDiagnostics, bool enableExplainMode, bool showSignalArrows, bool showHud, bool enableComparisonMode, bool enableDatasetEngine, SignalTier minTierForArrow)
        {
            return indicator.UltronV1(input, enableDiagnostics, enableExplainMode, showSignalArrows, showHud, enableComparisonMode, enableDatasetEngine, minTierForArrow);
        }
    }
}
#endregion
