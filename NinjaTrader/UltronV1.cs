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
using global::UltronV1.Core.Domain;
using global::UltronV1.Core.Dataset;
using global::UltronV1.Core.Engine;
using global::UltronV1.Core.Mnq;
using global::UltronV1.Core.Signals;
using NinjaTrader.NinjaScript.Ultron;
#endregion

namespace NinjaTrader.NinjaScript.Indicators
{
    public class UltronV1 : Indicator
    {
        UltronEngine _engine;
        UltronNtLogger _logger;
        UltronComparisonTracker _comparison;
        UltronIndicatorBundle _indicators = new UltronIndicatorBundle();
        UltronVwapTracker _vwap = new UltronVwapTracker();
        bool _vwapResetToday;

        const string HudTag = "UltronV1_HUD";

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

                AccountName = "Sim101";
                ATMTemplateName = string.Empty;
                Quantity = 1;

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
                _logger = new UltronNtLogger(Print);
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

                _indicators.Create(this);

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
            UltronTickFeed.ProcessLastTick(_engine, e, (out double bid, out double ask) =>
            {
                bid = GetCurrentBid();
                ask = GetCurrentAsk();
            });
        }

        protected override void OnBarUpdate()
        {
            if (_engine == null || !_engine.IsContractValid)
                return;
            if (BarsInProgress != 0)
                return;
            if (CurrentBar < 25)
                return;
            if (!UltronWarmupRequirements.IsMtfSeriesReady(CurrentBars[1]))
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

            if (ShowHud && IsFirstTickOfBar)
                DrawHud(result);
        }

        IndicatorSnapshot BuildIndicatorSnapshot()
        {
            return _indicators.BuildSnapshot(_vwap, Close[0], Volume[0], _engine.Config.AccelLookbackBars);
        }

        void UpdateVwap()
        {
            int timeEt = ToTime(Times[0][0]);
            if (timeEt >= 93000 && !_vwapResetToday)
            {
                _vwap.Reset();
                _vwapResetToday = true;
            }
            if (timeEt < 93000)
                _vwapResetToday = false;

            _vwap.UpdateBar(High[0], Low[0], Close[0], Volume[0], treatZeroVolumeAsOne: true);
        }

        void DrawSignal(BarEvaluationResult result)
        {
            var sig = result.Signal!;
            string tag = "UltronV1_Sig_" + CurrentBar;
            string label = FormatSignalLabel(sig.Direction, sig.Tier);
            Brush color = sig.Tier == SignalTier.A ? Brushes.LimeGreen
                : sig.Tier == SignalTier.B ? Brushes.DodgerBlue
                : Brushes.Goldenrod;

            if (sig.Direction == TradeDirection.Long)
            {
                Draw.ArrowUp(this, tag, true, 0, Low[0] - 4 * TickSize, color);
                Draw.Text(this, tag + "_t", label, 0, Low[0] - 8 * TickSize, color);
            }
            else
            {
                Draw.ArrowDown(this, tag, true, 0, High[0] + 4 * TickSize, color);
                Draw.Text(this, tag + "_t", label, 0, High[0] + 8 * TickSize, color);
            }
        }

        static string FormatSignalLabel(TradeDirection direction, SignalTier tier)
        {
            string side = direction == TradeDirection.Long ? "BUY" : "SELL";
            string tierLabel = tier switch
            {
                SignalTier.A => "A+",
                SignalTier.B => "B+",
                SignalTier.C => "C+",
                _ => tier.ToString()
            };
            return side + " " + tierLabel;
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

            Draw.TextFixed(this, HudTag, sb.ToString().TrimEnd(),
                TextPosition.TopLeft,
                Brushes.White,
                new SimpleFont("Consolas", 11),
                Brushes.Transparent,
                new SolidColorBrush(Color.FromArgb(160, 20, 20, 30)),
                100);
        }

        #region Properties — Execution

        [NinjaScriptProperty]
        [TypeConverter(typeof(UltronAccountConverter))]
        [Display(Name = "Account", Order = 1, GroupName = "Execution",
            Description = "Account for manual order entry (ATM hotkey).")]
        public string AccountName { get; set; }

        [NinjaScriptProperty]
        [TypeConverter(typeof(UltronAtmTemplateConverter))]
        [Display(Name = "ATM Template", Order = 2, GroupName = "Execution",
            Description = "ATM strategy template for manual execution.")]
        public string ATMTemplateName { get; set; }

        [NinjaScriptProperty]
        [Range(1, 100)]
        [Display(Name = "Quantity", Order = 3, GroupName = "Execution",
            Description = "Default contract quantity for manual execution.")]
        public int Quantity { get; set; }

        #endregion

        #region Properties — Ultron V1

        [NinjaScriptProperty]
        [Display(Name = "Enable Diagnostics Log", Order = 1, GroupName = "Ultron V1",
            Description = "Bar diagnostics to Output Window.")]
        public bool EnableDiagnostics { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Enable Explain Mode", Order = 2, GroupName = "Ultron V1",
            Description = "Score breakdown and gate details in Output Window only (not on chart).")]
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
    }
}
