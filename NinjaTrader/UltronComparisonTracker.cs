using System;
using System.Globalization;
using System.IO;
using System.Text;
using UltronV1.Core.Signals;

namespace NinjaTrader.NinjaScript.Indicators
{
    /// <summary>
    /// Tracks Ultron vs NightRanger signal comparison (detects NR arrows by tag NR_SIGNAL_).
    /// Does not embed or copy NightRanger logic.
    /// </summary>
    internal sealed class UltronComparisonTracker
    {
        const string NrTagPrefix = "NR_SIGNAL_";

        int _ultronSignals;
        int _nrSignals;
        int _bothMatch;
        int _ultronOnly;
        int _nrOnly;

        StreamWriter? _csv;
        string? _csvPath;

        public int UltronSignals => _ultronSignals;
        public int NrSignals => _nrSignals;
        public int BothMatch => _bothMatch;
        public int UltronOnly => _ultronOnly;
        public int NrOnly => _nrOnly;

        public void OpenLog(string directory)
        {
            CloseLog();
            Directory.CreateDirectory(directory);
            _csvPath = Path.Combine(directory, "UltronV1_NR_Comparison.csv");
            bool exists = File.Exists(_csvPath);
            _csv = new StreamWriter(_csvPath, append: true, Encoding.UTF8);
            if (!exists)
            {
                _csv.WriteLine("BarTime,BarIndex,UltronDir,UltronScore,UltronTier,NRDir,MatchType,DeltaSec");
                _csv.Flush();
            }
        }

        public void CloseLog()
        {
            _csv?.Dispose();
            _csv = null;
        }

        public TradeDirection DetectNightRangerOnBar(NinjaTrader.NinjaScript.Indicator host, int barIndex)
        {
            string tag = NrTagPrefix + barIndex;
            foreach (var draw in host.DrawObjects)
            {
                if (draw.Tag == null || !draw.Tag.Equals(tag, StringComparison.Ordinal))
                    continue;

                string typeName = draw.GetType().Name;
                if (typeName.IndexOf("ArrowUp", StringComparison.OrdinalIgnoreCase) >= 0)
                    return TradeDirection.Long;
                if (typeName.IndexOf("ArrowDown", StringComparison.OrdinalIgnoreCase) >= 0)
                    return TradeDirection.Short;
            }

            return TradeDirection.None;
        }

        public void RecordBar(
            DateTime barTime,
            int barIndex,
            TradeDirection ultronDir,
            int ultronScore,
            SignalTier ultronTier,
            TradeDirection nrDir)
        {
            string match;
            if (ultronDir != TradeDirection.None && nrDir != TradeDirection.None)
            {
                if (ultronDir == nrDir)
                {
                    _bothMatch++;
                    _ultronSignals++;
                    _nrSignals++;
                    match = "Both";
                }
                else
                {
                    match = "Conflict";
                }
            }
            else if (ultronDir != TradeDirection.None)
            {
                _ultronOnly++;
                _ultronSignals++;
                match = "UltronOnly";
            }
            else if (nrDir != TradeDirection.None)
            {
                _nrOnly++;
                _nrSignals++;
                match = "NROnly";
            }
            else
            {
                return;
            }

            _csv?.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "{0:yyyy-MM-dd HH:mm:ss},{1},{2},{3},{4},{5},{6},0",
                barTime, barIndex, ultronDir, ultronScore, ultronTier, nrDir, match));
            _csv?.Flush();
        }

        public string FormatHudLine()
        {
            return string.Format(CultureInfo.InvariantCulture,
                "CMP U:{0} NR:{1} Both:{2} Uonly:{3} NRonly:{4}",
                _ultronSignals, _nrSignals, _bothMatch, _ultronOnly, _nrOnly);
        }
    }
}
