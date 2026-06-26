using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UltronV1.Core.Engine;

namespace UltronV1.Core.Dataset
{
    /// <summary>
    /// Append-only JSON Lines storage — primary ML dataset format.
    /// Layout: {root}/sessions/{sessionId}/opportunities.jsonl + outcomes.jsonl + manifest.json
    /// </summary>
    public sealed class JsonLinesDatasetSink : IDatasetSink
    {
        readonly string _rootPath;
        readonly IUltronLogger _logger;
        string? _sessionDir;
        StreamWriter? _opportunitiesWriter;
        StreamWriter? _outcomesWriter;
        DatasetManifest? _manifest;

        public JsonLinesDatasetSink(string rootPath, IUltronLogger? logger = null)
        {
            _rootPath = rootPath ?? throw new ArgumentNullException(nameof(rootPath));
            _logger = logger ?? NullUltronLogger.Instance;
        }

        public void OpenSession(DatasetManifest manifest)
        {
            ExecuteIo("OpenSession", () =>
            {
                CloseSessionInternal(logCloseErrors: false);
                _manifest = manifest;
                _sessionDir = Path.Combine(_rootPath, "sessions", manifest.SessionId);
                Directory.CreateDirectory(_sessionDir);

                string oppPath = Path.Combine(_sessionDir, manifest.OpportunitiesFile);
                _opportunitiesWriter = new StreamWriter(oppPath, append: false, Encoding.UTF8)
                {
                    AutoFlush = true
                };

                string outPath = Path.Combine(_sessionDir, manifest.OutcomesFile);
                _outcomesWriter = new StreamWriter(outPath, append: false, Encoding.UTF8)
                {
                    AutoFlush = true
                };

                WriteManifestInternal();
            });
        }

        public void AppendOpportunity(OpportunityRecord record)
        {
            if (_opportunitiesWriter == null)
                throw new InvalidOperationException("Dataset session not open.");

            ExecuteIo("AppendOpportunity", () =>
            {
                _opportunitiesWriter.WriteLine(OpportunityJsonWriter.SerializeOpportunity(record));
            });
        }

        public void AppendOutcome(TradeOutcome outcome)
        {
            if (_outcomesWriter == null)
                throw new InvalidOperationException("Dataset session not open.");

            ExecuteIo("AppendOutcome", () =>
            {
                _outcomesWriter.WriteLine(OpportunityJsonWriter.SerializeOutcome(outcome));
            });
        }

        public void UpdateManifest(DatasetManifest manifest)
        {
            ExecuteIo("UpdateManifest", () =>
            {
                _manifest = manifest;
                WriteManifestInternal();
            });
        }

        public void CloseSession()
        {
            CloseSessionInternal(logCloseErrors: true);
        }

        void CloseSessionInternal(bool logCloseErrors)
        {
            try
            {
                _opportunitiesWriter?.Dispose();
            }
            catch (Exception ex) when (logCloseErrors)
            {
                UltronStructuredLog.Warn(
                    _logger,
                    UltronLogComponents.JsonLinesDatasetSink,
                    "CloseOpportunitiesWriter",
                    ex,
                    UltronLogActions.Propagate,
                    IoContext("CloseSession"));
            }
            finally
            {
                _opportunitiesWriter = null;
            }

            try
            {
                _outcomesWriter?.Dispose();
            }
            catch (Exception ex) when (logCloseErrors)
            {
                UltronStructuredLog.Warn(
                    _logger,
                    UltronLogComponents.JsonLinesDatasetSink,
                    "CloseOutcomesWriter",
                    ex,
                    UltronLogActions.Propagate,
                    IoContext("CloseSession"));
            }
            finally
            {
                _outcomesWriter = null;
            }
        }

        void WriteManifestInternal()
        {
            if (_manifest == null || _sessionDir == null)
                return;

            string path = Path.Combine(_sessionDir, "manifest.json");
            File.WriteAllText(path, OpportunityJsonWriter.SerializeManifest(_manifest), Encoding.UTF8);
        }

        void ExecuteIo(string operation, Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                UltronStructuredLog.Error(
                    _logger,
                    UltronLogComponents.JsonLinesDatasetSink,
                    operation,
                    ex,
                    UltronLogActions.Propagate,
                    IoContext(operation));
                throw;
            }
        }

        Dictionary<string, string> IoContext(string operation)
        {
            return new Dictionary<string, string>(4)
            {
                ["rootPath"] = _rootPath,
                ["sessionDir"] = _sessionDir ?? string.Empty,
                ["sessionId"] = _manifest?.SessionId ?? string.Empty,
                ["operation"] = operation
            };
        }
    }

    /// <summary>In-memory sink for unit tests.</summary>
    public sealed class InMemoryDatasetSink : IDatasetSink
    {
        public List<OpportunityRecord> Opportunities { get; } = new List<OpportunityRecord>();
        public List<TradeOutcome> Outcomes { get; } = new List<TradeOutcome>();
        public DatasetManifest? Manifest { get; private set; }

        public void OpenSession(DatasetManifest manifest) => Manifest = manifest;

        public void AppendOpportunity(OpportunityRecord record) => Opportunities.Add(record);

        public void AppendOutcome(TradeOutcome outcome) => Outcomes.Add(outcome);

        public void UpdateManifest(DatasetManifest manifest) => Manifest = manifest;

        public void CloseSession() { }
    }
}
