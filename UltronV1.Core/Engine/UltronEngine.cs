using System;
using UltronV1.Core.Ml;
using UltronV1.Core.Dataset;
using UltronV1.Core.Domain;
using UltronV1.Core.Features;
using UltronV1.Core.Mnq;
using UltronV1.Core.OrderFlow;
using UltronV1.Core.Signals;

namespace UltronV1.Core.Engine
{
    /// <summary>
    /// Central orchestrator: tick ingestion → FeatureEngine → gates → score → signal → dataset.
    /// </summary>
    public sealed class UltronEngine
    {
        readonly MnqTickProcessor _tickProcessor;
        readonly FeatureEngine _featureEngine;
        readonly SignalEvaluator _signalEvaluator;
        readonly IUltronLogger _logger;
        readonly UltronConfig _config;
        readonly DatasetEngine? _dataset;

        bool _contractValid;
        long _ticksThisBar;
        string _instrumentName = string.Empty;

        public UltronEngine(UltronConfig? config = null, IUltronLogger? logger = null, DatasetEngine? dataset = null, IMlAdvisor? mlAdvisor = null)
        {
            _config = config ?? UltronConfig.Default;
            _tickProcessor = new MnqTickProcessor();
            _featureEngine = new FeatureEngine();
            _signalEvaluator = new SignalEvaluator(mlAdvisor);
            _logger = logger ?? NullUltronLogger.Instance;
            _dataset = dataset;
        }

        public UltronConfig Config => _config;
        public DatasetEngine? Dataset => _dataset;
        public bool IsContractValid => _contractValid;
        public long TotalTicksProcessed { get; private set; }
        public BarEvaluationResult? LastResult { get; private set; }

        public void Initialize(string instrumentFullName)
        {
            _instrumentName = instrumentFullName;
            var validation = MnqContractValidator.Validate(instrumentFullName);
            _contractValid = validation.IsValid;

            if (_contractValid)
            {
                _logger.Info(validation.Message);
                if (_config.EnableDatasetEngine && _dataset != null)
                {
                    _dataset.StartSession(instrumentFullName, _config);
                    _logger.Info($"[UltronV1 Dataset] Session started: {_dataset.SessionId}");
                }
            }
            else
            {
                _logger.Error(validation.Message);
            }
        }

        public void ProcessTick(in TickEvent tick)
        {
            if (!_contractValid)
                return;

            _tickProcessor.ProcessTick(tick);
            _ticksThisBar++;
            TotalTicksProcessed++;
        }

        public BarEvaluationResult ProcessBarClose(
            int barIndex,
            DateTime barTime,
            double open,
            double high,
            double low,
            double close,
            long barVolume,
            in IndicatorSnapshot indicators)
        {
            if (!_contractValid)
            {
                LastResult = new BarEvaluationResult { ContractValid = false };
                return LastResult;
            }

            OrderFlowState orderFlow = _tickProcessor.FinalizeBar();
            long ticksThisBar = _ticksThisBar;
            _ticksThisBar = 0;

            BarSnapshot snapshot = _featureEngine.BuildSnapshot(
                barIndex, barTime, open, high, low, close, barVolume,
                ticksThisBar, orderFlow, indicators, _config);

            EvaluationResult evaluation = _signalEvaluator.Evaluate(snapshot, _config);

            var result = new BarEvaluationResult
            {
                Snapshot = snapshot,
                Evaluation = evaluation,
                ContractValid = true
            };

            if (ShouldRecordDataset(evaluation))
                result.LastSignalId = _dataset!.RecordEvaluation(result, _instrumentName, _config);

            LastResult = result;
            LogBarEvaluation(result, ticksThisBar);
            return result;
        }

        bool ShouldRecordDataset(EvaluationResult evaluation)
        {
            if (_dataset == null || !_config.EnableDatasetEngine)
                return false;

            if (_config.RecordAllBars)
                return true;

            // Candidate-only mode: at least one side passed hard gates
            return evaluation.Gates.LongAllowed || evaluation.Gates.ShortAllowed;
        }

        public void ResetSession()
        {
            _tickProcessor.ResetSession();
            _featureEngine.Reset();
            _ticksThisBar = 0;
            LastResult = null;
        }

        public void Shutdown()
        {
            _dataset?.EndSession();
        }

        void LogBarEvaluation(BarEvaluationResult result, long ticksThisBar)
        {
            if (!_config.EnableDiagnosticsLog && !_config.EnableExplainLog)
                return;

            BarSnapshot snap = result.Snapshot;
            OrderFlowState of = snap.OrderFlow;

            if (_config.EnableExplainLog || (result.Signal != null && _config.EnableDiagnosticsLog))
            {
                string explain = result.Explanation.FormatExplainBlock().TrimEnd();
                if (!string.IsNullOrEmpty(explain))
                    _logger.Info("[UltronV1 Explain]\n" + explain);
            }

            if (!_config.EnableDiagnosticsLog)
                return;

            if (result.Signal != null)
            {
                _logger.Info(string.Format(
                    "[UltronV1] SIGNAL {0} Tier={1} score={2} arch={3} id={4} bar={5} time={6:HH:mm}",
                    result.Signal.Direction,
                    result.Signal.Tier,
                    result.Signal.Score,
                    result.Signal.Archetype,
                    result.LastSignalId ?? "-",
                    snap.BarIndex,
                    snap.BarTime));
            }

            _logger.Info(string.Format(
                "[UltronV1] Bar={0} Time={1:HH:mm} Close={2:F2} Delta={3} RVOL={4:F2} L={5}/{6} S={7}/{8} Regime={9}",
                snap.BarIndex,
                snap.BarTime,
                snap.Close,
                of.Delta,
                snap.Rvol,
                result.Score.LongScore,
                _config.ScoreMinEntry,
                result.Score.ShortScore,
                _config.ScoreMinEntry,
                result.Explanation.Regime));
        }
    }
}
