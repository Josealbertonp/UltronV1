#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Reflection;
using NinjaTrader.Cbi;
using NinjaTrader.Core;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using global::UltronV1.Core.Dataset;
using global::UltronV1.Core.Domain;
using global::UltronV1.Core.Engine;
using global::UltronV1.Core.Execution;
using global::UltronV1.Core.Mnq;
using global::UltronV1.Core.Signals;
using NinjaTrader.NinjaScript.Ultron;
using global::UltronML;
#endregion

namespace NinjaTrader.NinjaScript.Strategies
{
    /// <summary>
    /// Phase 5 — Outcome Collection &amp; Paper Execution.
    /// Consumes Ultron signals only (no new entry rules). Writes outcomes.jsonl for ML/Validation.
    /// Authoritative execution: <see cref="TradeCycleTracker"/> / PaperBarSim in Core.
    /// Sim101 orders (opt-in) are visual only and never write outcomes.
    /// Use on Replay / Sim101 / Paper — not for live capital.
    /// </summary>
    public class UltronV1Paper : Strategy
    {
        const string Sim101VisualLog = "Sim101 visual only — outcome source PaperBarSim";
        UltronEngine _engine;
        DatasetEngine _dataset;
        TradeCycleTracker _tradeCycle;
        UltronNtLogger _logger;
        UltronIndicatorBundle _indicators = new UltronIndicatorBundle();
        UltronVwapTracker _vwap = new UltronVwapTracker();
        UltronPipelineDiagnostics _pipelineDiag = new UltronPipelineDiagnostics();
        UltronExecutionTelemetry _executionTelemetry = new UltronExecutionTelemetry();
        UltronMlHost? _mlHost;
        readonly HashSet<string> _loggedOrderCreates = new HashSet<string>();
        MarketPosition _sim101DebugPrevPosition = MarketPosition.Flat;

        int _outcomesRecorded;
        bool _initDiagOnBarUpdateLogged;

        #region Init diagnostics (temporary)

        void InitDiagLogConfigSnapshot()
        {
            Print("[INIT] Account selecionada=" + (string.IsNullOrWhiteSpace(AccountName) ? "(vazio)" : AccountName));
            Print("[INIT] ExecutionMode=" + ExecutionMode);
            Print("[INIT] ATM Template=" + (string.IsNullOrWhiteSpace(AtmTemplateName) ? "(vazio)" : AtmTemplateName));
            Print("[INIT] Shadow ML habilitado=" + EnableShadowMl);
            Print("[INIT] MlFilterMode=" + MlFilterMode);
            Print("[INIT] EnableMlFilter=" + EnableMlFilter);
            Print("[INIT] Caminho do modelo (property)=" + (string.IsNullOrWhiteSpace(ShadowMlModelDirectory) ? "(default champion)" : ShadowMlModelDirectory));
        }

        void InitDiagProbeOnnxDll()
        {
            try
            {
                Print("[INIT] Antes de carregamento DLL ONNX");
                string dllPath = Path.Combine(
                    NinjaTrader.Core.Globals.UserDataDir,
                    "bin", "Custom", "Microsoft.ML.OnnxRuntime.dll");
                if (File.Exists(dllPath))
                {
                    Assembly onnxAsm = Assembly.LoadFrom(dllPath);
                    Print("[INIT] DLL ONNX carregada: " + onnxAsm.FullName);
                    Print("[INIT] DLL ONNX location: " + onnxAsm.Location);
                }
                else
                {
                    Print("[INIT WARN] DLL ONNX nao encontrada em: " + dllPath);
                }
                Print("[INIT] Depois de carregamento DLL ONNX");
            }
            catch (Exception ex)
            {
                Print("[INIT WARN] carregamento DLL ONNX (diagnostico): " + ex.Message);
            }
        }

        void InitDiagValidateAtm()
        {
            try
            {
                Print("[INIT] Antes de InitializeAtm()");
                string template = (AtmTemplateName ?? string.Empty).Trim();
                bool templateConfigured = !string.IsNullOrWhiteSpace(template);
                bool templateExists = templateConfigured && AtmTemplateFileExists(template);
                Account? account = ResolveExecutionAccount();
                string accountStatus = account == null
                    ? "nao encontrada"
                    : (account.Connection != null && account.Connection.Status == ConnectionStatus.Connected
                        ? "conectada"
                        : "desconectada");

                Print("[INIT] ATM Template validation configured=" + templateConfigured);
                Print("[INIT] ATM Template validation exists=" + templateExists);
                Print("[INIT] ATM Template validation result=" + (templateExists ? "OK" : (templateConfigured ? "NOT_FOUND" : "NOT_CONFIGURED")));
                Print("[INIT] ATM account resolve=" + (account?.Name ?? "(null)") + " status=" + accountStatus);
                Print("[INIT] Depois de InitializeAtm()");
            }
            catch (Exception ex)
            {
                Print("[INIT WARN] InitializeAtm (diagnostico): " + ex.Message);
            }
        }

        #endregion

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Print("[INIT] Entrou em State.SetDefaults");
                try
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
                    MinTierForEntry = 2;
                    ExecutionMode = UltronPaperExecutionMode.PaperBarSim;
                    AccountName = "Sim101";
                    AtmTemplateName = string.Empty;
                    Quantity = 1;
                    EnablePipelineDiagnostics = true;
                    EnableExecutionTelemetry = true;
                    ExecutionLogToOutput = false;
                    EnableSim101DebugLog = true;
                    EnableShadowMl = false;
                    ShadowMlModelDirectory = string.Empty;
                    MlFilterMode = UltronMlFilterMode.Disabled;
                    EnableMlFilter = false;
                    ForcePassThrough = false;
                    MlUseAutoThreshold = true;
                    MlThresholdManual = 0.70;
                    Print("[INIT] Depois de State.SetDefaults");
                }
                catch (Exception ex)
                {
                    Print("[INIT ERROR] State.SetDefaults: " + ex);
                    throw;
                }
            }
            else if (State == State.Configure)
            {
                Print("[INIT] Entrou em State.Configure");
                try
                {
                    AddDataSeries(BarsPeriodType.Minute, MnqContractSpec.MtfBarPeriodMinutes);
                    Print("[INIT] Depois de State.Configure");
                }
                catch (Exception ex)
                {
                    Print("[INIT ERROR] State.Configure: " + ex);
                    throw;
                }
            }
            else if (State == State.DataLoaded)
            {
                Print("[INIT] Entrou em State.DataLoaded");
                try
                {
                    _logger = new UltronNtLogger(Print);
                    InitDiagLogConfigSnapshot();

                    UltronConfig cfg;
                    try
                    {
                        Print("[INIT] Antes de InitializeDatasetEngine()");
                        cfg = new UltronConfig
                        {
                            EnableDiagnosticsLog = true,
                            EnableExplainLog = false,
                            EnableDatasetEngine = true,
                            EnableOutcomeCollection = true,
                            MinSignalTier = (SignalTier)MinTierForEntry,
                            PaperStopLossTicks = PaperStopLossTicks,
                            PaperTakeProfitTicks = PaperTakeProfitTicks,
                            PaperMaxBarsInTrade = PaperMaxBarsInTrade,
                            ConfigVersion = UltronVersions.DefaultConfigVersion
                        };

                        string root = Path.Combine(NinjaTrader.Core.Globals.UserDataDir, "UltronV1", "dataset");
                        _dataset = new DatasetEngine(new JsonLinesDatasetSink(root, _logger), _logger);
                        Print("[INIT] Depois de InitializeDatasetEngine() session=" + _dataset.SessionId);
                    }
                    catch (Exception ex)
                    {
                        Print("[INIT ERROR] InitializeDatasetEngine: " + ex);
                        throw;
                    }

                    try
                    {
                        Print("[INIT] Antes de InitializeTradeCycle()");
                        _tradeCycle = new TradeCycleTracker(new PaperExecutionConfig
                        {
                            StopLossTicks = PaperStopLossTicks,
                            TakeProfitTicks = PaperTakeProfitTicks,
                            MaxBarsInTrade = PaperMaxBarsInTrade,
                            ExecutionMode = "PaperBarSim"
                        });
                        Print("[INIT] Depois de InitializeTradeCycle()");
                    }
                    catch (Exception ex)
                    {
                        Print("[INIT ERROR] InitializeTradeCycle: " + ex);
                        throw;
                    }

                    try
                    {
                        Print("[INIT] Antes de InitializeExecution()");
                        _engine = new UltronEngine(cfg, _logger, _dataset);
                        _engine.Initialize(Instrument.FullName);
                        Print("[INIT] Depois de InitializeExecution() contractValid=" + _engine.IsContractValid);
                    }
                    catch (Exception ex)
                    {
                        Print("[INIT ERROR] InitializeExecution: " + ex);
                        throw;
                    }

                    if (!_engine.IsContractValid)
                        Print("[UltronV1Paper] INVALID CONTRACT — MNQ 09-26 required.");

                    _indicators.Create(this);

                    if (EnablePipelineDiagnostics)
                    {
                        string diagDir = Path.Combine(NinjaTrader.Core.Globals.UserDataDir, "UltronV1", "diagnostics");
                        _pipelineDiag.OpenLog(diagDir, "UltronV1Paper_" + _dataset.SessionId);
                        Print("[UltronV1Paper] Pipeline diagnostics log: " + _pipelineDiag.LogFilePath);
                    }

                    if (EnableExecutionTelemetry)
                    {
                        string diagDir = Path.Combine(NinjaTrader.Core.Globals.UserDataDir, "UltronV1", "diagnostics");
                        _executionTelemetry.OpenLog(
                            diagDir,
                            "UltronV1Paper_" + _dataset.SessionId,
                            ExecutionLogToOutput ? (Action<string>)Print : null);
                        Print("[UltronV1Paper] Execution telemetry log: " + _executionTelemetry.LogFilePath);
                    }

                    LogPaperExecutionMode();
                    InitDiagValidateAtm();

                    string datasetRoot = Path.Combine(NinjaTrader.Core.Globals.UserDataDir, "UltronV1", "dataset");
                    if (EnableShadowMl || EnableMlFilter || MlFilterMode != UltronMlFilterMode.Disabled)
                        InitializeMlHost(datasetRoot);
                    else
                        Print("[INIT] InitializeMlHost() não invocado — ML desabilitado (evita carga ONNX)");
                    Print(string.Format("[UltronV1Paper] Outcome collection started. Session={0}", _dataset.SessionId));
                    Print("[INIT] Depois de State.DataLoaded");
                }
                catch (Exception ex)
                {
                    Print("[INIT ERROR] State.DataLoaded: " + ex);
                    throw;
                }
            }
            else if (State == State.Historical)
            {
                Print("[INIT] Entrou em State.Historical");
            }
            else if (State == State.Realtime)
            {
                Print("[INIT] Entrou em State.Realtime");
            }
            else if (State == State.Terminated)
            {
                Print("[INIT] Entrou em State.Terminated");
                try
                {
                    if (EnablePipelineDiagnostics)
                    {
                        _pipelineDiag.PrintSummary();
                        string logPath = _pipelineDiag.LogFilePath ?? "(none)";
                        _pipelineDiag.CloseLog();
                        Print("[UltronV1Paper] Pipeline diagnostics saved: " + logPath);
                    }

                    if (EnableExecutionTelemetry)
                    {
                        string execPath = _executionTelemetry.LogFilePath ?? "(none)";
                        _executionTelemetry.CloseLog();
                        Print("[UltronV1Paper] Execution telemetry saved: " + execPath);
                    }

                    Print(string.Format("[UltronV1Paper] Session ended. Outcomes recorded={0}", _outcomesRecorded));
                    _mlHost?.Dispose();
                    _mlHost = null;
                    _engine?.Shutdown();
                    _engine = null;
                    _dataset = null;
                    _tradeCycle = null;
                    Print("[INIT] Depois de State.Terminated");
                }
                catch (Exception ex)
                {
                    Print("[INIT ERROR] State.Terminated: " + ex);
                    throw;
                }
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
            if (!_initDiagOnBarUpdateLogged)
            {
                _initDiagOnBarUpdateLogged = true;
                Print("[INIT]");
                Print("Strategy initialized successfully");
            }

            if (_engine == null || !_engine.IsContractValid)
                return;
            if (BarsInProgress != 0)
                return;
            if (CurrentBar < BarsRequiredToTrade)
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

            ProcessOutcomeCycle(result);

            if (EnablePipelineDiagnostics && IsFirstTickOfBar)
                _pipelineDiag.RecordBar(
                    result,
                    _engine.Config,
                    (SignalTier)MinTierForEntry,
                    _tradeCycle.IsOpen);

            if (result.Signal != null && result.Signal.Tier >= (SignalTier)MinTierForEntry && !_tradeCycle.IsOpen)
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

            if (EnableExecutionTelemetry)
                _executionTelemetry.CloseTrade(
                    outcome.SignalId,
                    outcome.ExitReason,
                    outcome.RealizedPnLTicks,
                    outcome.BarsInTrade);

            if (_dataset.EnrichOutcome(outcome))
            {
                _outcomesRecorded++;
                _mlHost?.OnPassedOutcome(outcome, ResolveAtmLabel());
                Print(string.Format(
                    "[UltronV1Paper] OUTCOME {0} id={1} R={2:F2} PnL={3:F1}t reason={4}",
                    outcome.IsWin ? "WIN" : "LOSS",
                    outcome.SignalId,
                    outcome.RMultiple,
                    outcome.RealizedPnLTicks,
                    outcome.ExitReason));
            }

            if (ExecutionMode == UltronPaperExecutionMode.Sim101Visual
                && Position.MarketPosition != MarketPosition.Flat)
                TrySubmitSim101VisualExit();
        }

        void LogPaperExecutionMode()
        {
            Print("[UltronV1Paper] Outcome source: TradeCycleTracker / PaperBarSim (authoritative)");
            switch (ExecutionMode)
            {
                case UltronPaperExecutionMode.Sim101Visual:
                    Print("[UltronV1Paper] " + Sim101VisualLog);
                    break;
                case UltronPaperExecutionMode.NinjaTraderAtm:
                    Print("[UltronV1Paper] NinjaTrader ATM visual only — outcome source PaperBarSim");
                    Print(string.Format(
                        "[UltronV1Paper] ATM template={0} account={1} qty={2}",
                        string.IsNullOrWhiteSpace(AtmTemplateName) ? "(not set)" : AtmTemplateName,
                        string.IsNullOrWhiteSpace(AccountName) ? "(not set)" : AccountName,
                        Quantity));
                    break;
            }
        }

        /// <summary>Sim101 broker orders for chart visibility only — not used for outcomes.jsonl.</summary>
        void TrySubmitSim101VisualEntry(TradeDirection direction)
        {
            if (direction == TradeDirection.Long)
            {
                LogSim101BeforeEntry("EnterLong");
                EnterLong(1, "UV1_L");
                LogSim101AfterEntry("EnterLong");
                SetStopLoss(CalculationMode.Ticks, PaperStopLossTicks);
                SetProfitTarget(CalculationMode.Ticks, PaperTakeProfitTicks);
            }
            else
            {
                LogSim101BeforeEntry("EnterShort");
                EnterShort(1, "UV1_S");
                LogSim101AfterEntry("EnterShort");
                SetStopLoss(CalculationMode.Ticks, PaperStopLossTicks);
                SetProfitTarget(CalculationMode.Ticks, PaperTakeProfitTicks);
            }
        }

        void LogSim101BeforeEntry(string methodName)
        {
            if (!EnableSim101DebugLog)
                return;

            Print("[SIM101]");
            Print("Calling " + methodName + "()");
            Print("State=" + State);
            Print("Historical=" + (State == State.Historical));
            Print("Realtime=" + (State == State.Realtime));
            Print("BarsInProgress=" + BarsInProgress);
            Print("CurrentBar=" + CurrentBar);
            Print("Account=" + Sim101AccountLabel());
            Print("Position=" + Position.MarketPosition
                + " qty=" + Position.Quantity
                + " avg=" + Position.AveragePrice.ToString("F2"));
        }

        void LogSim101AfterEntry(string methodName)
        {
            if (!EnableSim101DebugLog)
                return;

            Print("[SIM101]");
            Print(methodName + "() returned");
            Print("Position=" + Position.MarketPosition
                + " qty=" + Position.Quantity
                + " avg=" + Position.AveragePrice.ToString("F2"));
        }

        static string Sim101AccountLabel()
        {
            try
            {
                lock (Account.All)
                {
                    foreach (Account acct in Account.All)
                    {
                        if (acct == null)
                            continue;
                        if (acct.Connection != null && acct.Connection.Status == ConnectionStatus.Connected)
                            return acct.Name;
                    }
                }
            }
            catch (Exception)
            {
                /* design-time / Account.All unavailable */
            }
            return "(unknown)";
        }

        static string? FormatPositionTransition(MarketPosition from, MarketPosition to)
        {
            if (from == to)
                return null;
            return from + " -> " + to;
        }

        void TrySubmitVisualEntry(TradeDirection direction)
        {
            switch (ExecutionMode)
            {
                case UltronPaperExecutionMode.Sim101Visual:
                    TrySubmitSim101VisualEntry(direction);
                    break;
                case UltronPaperExecutionMode.NinjaTraderAtm:
                    TrySubmitNinjaTraderAtmEntry(direction);
                    break;
            }
        }

        /// <summary>
        /// NinjaTrader ATM via account order + StartAtmStrategy (native ATM, visual only).
        /// Quantity comes from strategy <see cref="Quantity"/> property.
        /// PaperBarSim is unaffected if this fails.
        /// </summary>
        void TrySubmitNinjaTraderAtmEntry(TradeDirection direction)
        {
            const string entryOrderName = "Entry";
            string template = (AtmTemplateName ?? string.Empty).Trim();
            string accountLabel = AccountName ?? string.Empty;
            int qty = Math.Max(1, Quantity);

            if (string.IsNullOrWhiteSpace(template))
            {
                LogAtmFailed("ATM template not configured");
                return;
            }

            if (!AtmTemplateFileExists(template))
            {
                Print("[ATM]");
                Print("Template not found:");
                Print(template);
                return;
            }

            if (State == State.Historical)
            {
                LogAtmFailed("ATM does not run on historical bars (NT8 limitation)");
                return;
            }

            Account? account = ResolveExecutionAccount();
            if (account == null)
            {
                LogAtmFailed("Account not found: " + accountLabel);
                return;
            }

            if (account.Connection == null || account.Connection.Status != ConnectionStatus.Connected)
            {
                LogAtmFailed("Account not connected: " + account.Name);
                return;
            }

            Print("[ATM]");
            Print("Creating ATM...");
            Print("Account: " + account.Name);
            Print("Template: " + template);
            Print("Quantity: " + qty);
            Print("Instrument: " + Instrument.FullName);
            Print("Order Name:");
            Print(entryOrderName);

            OrderAction action = direction == TradeDirection.Long
                ? OrderAction.Buy
                : OrderAction.SellShort;

            try
            {
                Order entryOrder = account.CreateOrder(
                    Instrument,
                    action,
                    OrderType.Market,
                    OrderEntry.Automated,
                    TimeInForce.Gtc,
                    qty,
                    0,
                    0,
                    string.Empty,
                    entryOrderName,
                    Globals.MaxDate,
                    null);

                NinjaTrader.NinjaScript.AtmStrategy.StartAtmStrategy(template, entryOrder);

                Print("[ATM]");
                Print("ATM Start Requested");
            }
            catch (Exception ex)
            {
                LogAtmFailed(ex.Message);
            }
        }

        static bool AtmTemplateFileExists(string templateName)
        {
            string path = Path.Combine(
                Globals.UserDataDir,
                "templates",
                "AtmStrategy",
                templateName + ".xml");
            return File.Exists(path);
        }

        void LogAtmFailed(string error)
        {
            Print("[ATM]");
            Print("ATM Failed");
            Print("Erro=" + error);
        }

        Account? ResolveExecutionAccount()
        {
            if (string.IsNullOrWhiteSpace(AccountName))
                return null;

            try
            {
                lock (Account.All)
                {
                    foreach (Account acct in Account.All)
                    {
                        if (acct == null)
                            continue;
                        if (acct.Name.Equals(AccountName, StringComparison.OrdinalIgnoreCase)
                            || acct.DisplayName.Equals(AccountName, StringComparison.OrdinalIgnoreCase))
                            return acct;
                    }
                }
            }
            catch (Exception)
            {
                /* Account.All unavailable at design time */
            }

            return null;
        }

        /// <summary>Sim101 broker exit for chart visibility only — not used for outcomes.jsonl.</summary>
        void TrySubmitSim101VisualExit()
        {
            if (Position.MarketPosition == MarketPosition.Long)
                ExitLong("UV1_Exit", "UV1_L");
            else if (Position.MarketPosition == MarketPosition.Short)
                ExitShort("UV1_Exit", "UV1_S");
        }

        void TryOpenPaperTrade(BarEvaluationResult result)
        {
            if (result.Signal == null || string.IsNullOrEmpty(result.LastSignalId))
                return;

            MlFilterResult? mlFilter = _mlHost?.EvaluateEntry(result, Time[0]);
            if (mlFilter != null && !mlFilter.AllowEntry)
            {
                _mlHost!.OnBlockedSignal(result.LastSignalId!);
                if (EnableExecutionTelemetry)
                    _executionTelemetry.TryOpenResult(false, "MlFilterBlocked");
                Print(string.Format(
                    "[UltronV1Paper] ML FILTER BLOCK id={0} score={1} tier={2} conf={3:F2}",
                    result.LastSignalId,
                    result.Signal.Score,
                    result.Signal.Tier,
                    mlFilter.Prediction.Confidence));
                return;
            }

            const int quantity = 1;
            double entry = Close[0];
            TradeDirection direction = result.Signal.Direction;

            if (EnableExecutionTelemetry)
                _executionTelemetry.BeforeTryOpen(direction.ToString(), entry, quantity);

            string? failReason = ClassifyTryOpenFail(_tradeCycle, result.LastSignalId, direction);
            if (failReason != null)
            {
                if (EnableExecutionTelemetry)
                    _executionTelemetry.TryOpenResult(false, failReason);
                return;
            }

            bool opened = _tradeCycle.TryOpen(
                result.LastSignalId,
                direction,
                entry,
                Time[0].ToUniversalTime(),
                CurrentBar);

            if (EnableExecutionTelemetry)
                _executionTelemetry.TryOpenResult(opened, opened ? null : "TryOpenReturnedFalse");

            if (!opened)
                return;

            if (EnablePipelineDiagnostics)
                _pipelineDiag.RecordEntrySubmitted();

            Print(string.Format(
                "[UltronV1Paper] OPEN {0} id={1} score={2} tier={3} entry={4:F2}",
                direction,
                result.LastSignalId,
                result.Signal.Score,
                result.Signal.Tier,
                entry));

            if (ExecutionMode == UltronPaperExecutionMode.PaperBarSim)
                return;

            TrySubmitVisualEntry(direction);
        }

        static string? ClassifyTryOpenFail(TradeCycleTracker tracker, string signalId, TradeDirection direction)
        {
            if (tracker.IsOpen)
                return "TradeCycleAlreadyOpen";
            if (string.IsNullOrEmpty(signalId))
                return "EmptySignalId";
            if (direction == TradeDirection.None)
                return "DirectionNone";
            return null;
        }

        protected override void OnPositionUpdate(
            Position position,
            double averagePrice,
            int quantity,
            MarketPosition marketPosition)
        {
            if (EnableSim101DebugLog)
            {
                string? transition = FormatPositionTransition(_sim101DebugPrevPosition, marketPosition);
                if (transition != null)
                {
                    Print("[SIM101]");
                    Print("Position transition: " + transition);
                    Print("AveragePrice=" + averagePrice.ToString("F2"));
                    Print("Quantity=" + quantity);
                }
                _sim101DebugPrevPosition = marketPosition;
            }

            if (EnableExecutionTelemetry)
                _executionTelemetry.PositionChanged(marketPosition, averagePrice, quantity);
        }

        protected override void OnOrderUpdate(
            Order order,
            double limitPrice,
            double stopPrice,
            int quantity,
            int filled,
            double averageFillPrice,
            OrderState orderState,
            DateTime time,
            ErrorCode error,
            string nativeError)
        {
            if (EnableSim101DebugLog && order != null)
            {
                string acct = order.Account != null ? order.Account.Name : "(null)";
                Print("[SIM101]");
                Print("OnOrderUpdate");
                Print("OrderState=" + orderState);
                Print("Error=" + error);
                Print("NativeError=" + (nativeError ?? string.Empty));
                Print("Name=" + (order.Name ?? string.Empty));
                Print("Quantity=" + quantity);
                Print("Filled=" + filled);
                Print("AverageFillPrice=" + averageFillPrice.ToString("F2"));
                Print("Account=" + acct);
                Print("Time=" + time.ToString("O"));
            }

            if (!EnableExecutionTelemetry || order == null)
                return;

            string orderId = string.IsNullOrEmpty(order.Name) ? "order" : order.Name;

            if ((orderState == OrderState.Working || orderState == OrderState.Accepted)
                && filled == 0
                && _loggedOrderCreates.Add(orderId))
            {
                _executionTelemetry.OrderCreated(
                    orderId,
                    orderState,
                    order.OrderAction.ToString(),
                    quantity);
            }

            if (orderState == OrderState.Filled)
            {
                _executionTelemetry.OrderFilled(orderId, averageFillPrice, time, filled);
                return;
            }

            if (orderState == OrderState.Cancelled || orderState == OrderState.Rejected)
            {
                string reason = error != ErrorCode.NoError
                    ? error.ToString()
                    : (string.IsNullOrEmpty(nativeError) ? orderState.ToString() : nativeError);
                _executionTelemetry.OrderCancelled(orderId, reason, orderState);
            }
        }

        protected override void OnExecutionUpdate(
            Execution execution,
            string executionId,
            double price,
            int quantity,
            MarketPosition marketPosition,
            string orderId,
            DateTime time)
        {
            if (!EnableSim101DebugLog)
                return;

            string orderName = execution?.Order != null && !string.IsNullOrEmpty(execution.Order.Name)
                ? execution.Order.Name
                : orderId;

            Print("[SIM101]");
            Print("Execution received");
            Print("Price=" + price.ToString("F2"));
            Print("Quantity=" + quantity);
            Print("MarketPosition=" + marketPosition);
            Print("Order.Name=" + orderName);
            Print("ExecutionId=" + executionId);
            Print("Time=" + time.ToString("O"));
        }

        IndicatorSnapshot BuildIndicatorSnapshot()
        {
            return _indicators.BuildSnapshot(_vwap, Close[0], Volume[0], _engine.Config.AccelLookbackBars);
        }

        void UpdateVwap()
        {
            if (Bars.IsFirstBarOfSession)
                _vwap.Reset();

            _vwap.UpdateBar(High[0], Low[0], Close[0], Volume[0], treatZeroVolumeAsOne: false);
        }

        void InitializeMlHost(string datasetRoot)
        {
            Print("[INIT] Antes de InitializeMlHost()");
            if (MlFilterMode == UltronMlFilterMode.Disabled && !EnableShadowMl && !EnableMlFilter)
            {
                Print("[INIT] InitializeMlHost() ignorado — ML desabilitado");
                Print("[INIT] Depois de InitializeMlHost()");
                return;
            }

            string modelDir;
            try
            {
                Print("[INIT] Antes de acesso ao diretório Shadow ML");
                modelDir = ResolveShadowModelDirectory(datasetRoot);
                Print("[INIT] Caminho do modelo resolvido=" + modelDir);
                Print("[INIT] Diretório Shadow ML existe=" + Directory.Exists(modelDir));
                if (Directory.Exists(modelDir))
                {
                    Print("[INIT] model.onnx existe=" + File.Exists(Path.Combine(modelDir, "model.onnx")));
                    Print("[INIT] thresholds.json existe=" + File.Exists(Path.Combine(modelDir, "thresholds.json")));
                    Print("[INIT] metadata.json existe=" + File.Exists(Path.Combine(modelDir, "metadata.json")));
                }
                Print("[INIT] Depois de acesso ao diretório Shadow ML");
            }
            catch (Exception ex)
            {
                Print("[ML FILTER] Shadow ML path error — strategy continues: " + ex.Message);
                Print("[INIT] Depois de InitializeMlHost()");
                return;
            }

            InitDiagProbeOnnxDll();

            var hostConfig = new MlHostConfig
            {
                Mode = (MlFilterMode)MlFilterMode,
                EnableShadowMl = EnableShadowMl,
                EnableMlFilter = EnableMlFilter,
                ForcePassThrough = ForcePassThrough,
                ModelDirectory = modelDir,
                ThresholdOverride = MlUseAutoThreshold ? null : (double?)MlThresholdManual
            };

            try
            {
                Print("[INIT] Antes de carregamento de modelo");
                _mlHost = new UltronMlHost(hostConfig, Print);
                string sessionDir = Path.Combine(datasetRoot, "sessions", _dataset.SessionId);
                _mlHost.OpenSession(sessionDir);
                Print("[INIT] Modelo carregado loadError=" + (_mlHost.LoadError ?? "(nenhum)"));
                Print("[INIT] Depois de carregamento de modelo");
            }
            catch (Exception ex)
            {
                Print("[ML FILTER] Load failed — strategy continues without ML: " + ex.Message);
                _mlHost?.Dispose();
                _mlHost = null;
            }

            if (_mlHost != null && _mlHost.LoadError != null)
                Print("[ML FILTER] Load warning: " + _mlHost.LoadError);
            else if (_mlHost != null)
                Print(string.Format(
                    "[ML FILTER] Mode={0} EnableFilter={1} ForcePass={2} ModelDir={3}",
                    MlFilterMode, EnableMlFilter, ForcePassThrough, modelDir));

            Print("[INIT] Depois de InitializeMlHost()");
        }

        string ResolveShadowModelDirectory(string datasetRoot)
        {
            if (!string.IsNullOrWhiteSpace(ShadowMlModelDirectory))
            {
                if (Path.IsPathRooted(ShadowMlModelDirectory))
                    return ShadowMlModelDirectory;
                return Path.Combine(NinjaTrader.Core.Globals.UserDataDir, ShadowMlModelDirectory);
            }

            return Path.Combine(
                NinjaTrader.Core.Globals.UserDataDir,
                "UltronV1",
                "UltronML",
                "models",
                "champion");
        }

        string? ResolveAtmLabel()
        {
            if (ExecutionMode != UltronPaperExecutionMode.NinjaTraderAtm)
                return ExecutionMode.ToString();
            return string.IsNullOrWhiteSpace(AtmTemplateName) ? "ATM" : AtmTemplateName;
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
        [Range(0, 3)]
        [Display(Name = "Min Tier For Entry", Order = 4, GroupName = "Ultron Paper")]
        public int MinTierForEntry { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Execution Mode", Order = 1, GroupName = "Execution",
            Description = "PaperBarSim = no broker orders. Sim101Visual = EnterLong bracket. NinjaTraderAtm = ATM template.")]
        public UltronPaperExecutionMode ExecutionMode { get; set; }

        [NinjaScriptProperty]
        [TypeConverter(typeof(UltronAccountConverter))]
        [Display(Name = "Account", Order = 2, GroupName = "Execution",
            Description = "Account for NinjaTrader ATM visual orders.")]
        public string AccountName { get; set; }

        [NinjaScriptProperty]
        [TypeConverter(typeof(UltronAtmTemplateConverter))]
        [Display(Name = "ATM Template", Order = 3, GroupName = "Execution",
            Description = "Template name (XML filename without extension) from templates\\AtmStrategy.")]
        public string AtmTemplateName { get; set; }

        [NinjaScriptProperty]
        [Range(1, 100)]
        [Display(Name = "Quantity", Order = 4, GroupName = "Execution",
            Description = "Contracts sent to ATM entry order (ATM mode only).")]
        public int Quantity { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Enable Pipeline Diagnostics", Order = 6, GroupName = "Ultron Paper",
            Description = "Bar-close funnel counters (file log under UltronV1/diagnostics/).")]
        public bool EnablePipelineDiagnostics { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Enable Execution Telemetry", Order = 7, GroupName = "Ultron Paper",
            Description = "Post-TryOpen execution events (PaperBarSim + Sim101 orders).")]
        public bool EnableExecutionTelemetry { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Execution Log To Output", Order = 8, GroupName = "Ultron Paper",
            Description = "Mirror ULTRON EXECUTION blocks to Output Window (default: file only).")]
        public bool ExecutionLogToOutput { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Enable Sim101 Debug Log (temp)", Order = 9, GroupName = "Ultron Paper",
            Description = "Temporary: [SIM101] traces for EnterLong/OnOrderUpdate/OnExecutionUpdate.")]
        public bool EnableSim101DebugLog { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Enable Shadow ML", Order = 1, GroupName = "Shadow ML",
            Description = "ONNX inference PASS_THROUGH — never blocks trades (RC2 Phase 4A).")]
        public bool EnableShadowMl { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Shadow ML Model Directory", Order = 2, GroupName = "Shadow ML",
            Description = "Champion folder with model.onnx. Empty = UltronV1/UltronML/models/champion")]
        public string ShadowMlModelDirectory { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "ML Filter Mode", Order = 1, GroupName = "ML Filter",
            Description = "Disabled=RC1 | Shadow=observe only | Active=may veto entries")]
        public UltronMlFilterMode MlFilterMode { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Enable ML Filter", Order = 2, GroupName = "ML Filter",
            Description = "Active mode only — block low-confidence entries. Default false.")]
        public bool EnableMlFilter { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Force Pass Through", Order = 3, GroupName = "ML Filter",
            Description = "Emergency rollback — all trades pass instantly.")]
        public bool ForcePassThrough { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Use Auto Threshold", Order = 4, GroupName = "ML Filter",
            Description = "Load threshold from thresholds.json. If false, use manual value.")]
        public bool MlUseAutoThreshold { get; set; }

        [NinjaScriptProperty]
        [Range(0.40, 0.95)]
        [Display(Name = "Manual Threshold", Order = 5, GroupName = "ML Filter",
            Description = "Used when auto threshold is off (e.g. 0.70).")]
        public double MlThresholdManual { get; set; }

        #endregion
    }
}
