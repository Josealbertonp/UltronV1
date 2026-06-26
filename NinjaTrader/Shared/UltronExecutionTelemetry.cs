using System;
using System.Globalization;
using System.IO;
using System.Text;
using NinjaTrader.Cbi;

namespace NinjaTrader.NinjaScript.Ultron
{
    /// <summary>
    /// Post-entry execution telemetry (PaperBarSim + optional Sim101 orders). File-only — no trading logic.
    /// </summary>
    public sealed class UltronExecutionTelemetry
    {
        StreamWriter? _writer;
        Action<string>? _output;
        MarketPosition _lastMarketPosition = MarketPosition.Flat;
        int _lastQuantity;
        double _lastAveragePrice;

        public string? LogFilePath { get; private set; }

        public void OpenLog(string directory, string fileStem, Action<string>? mirrorToOutput = null)
        {
            CloseLog();
            _output = mirrorToOutput;
            Directory.CreateDirectory(directory);
            LogFilePath = Path.Combine(directory, fileStem + "_execution.log");
            _writer = new StreamWriter(LogFilePath, append: false, Encoding.UTF8)
            {
                AutoFlush = true
            };
            WriteBlock("SESSION START", DateTime.Now.ToString("O", CultureInfo.InvariantCulture));
        }

        public void CloseLog()
        {
            if (_writer != null)
            {
                WriteBlock("SESSION END", DateTime.Now.ToString("O", CultureInfo.InvariantCulture));
                _writer.Dispose();
                _writer = null;
            }
            _output = null;
        }

        public void BeforeTryOpen(string direction, double price, int quantity)
        {
            WriteBlock("BEFORE TRYOPEN",
                "Direction: " + direction,
                "Price: " + price.ToString("F2", CultureInfo.InvariantCulture),
                "Quantity: " + quantity);
        }

        public void TryOpenResult(bool success, string? failReason)
        {
            if (success)
                WriteBlock("TRYOPEN RESULT", "Success: true");
            else
                WriteBlock("TRYOPEN RESULT", "Success: false", "Reason: " + (failReason ?? "unknown"));
        }

        public void CloseTrade(string signalId, string reason, double pnlTicks, int barsInTrade)
        {
            WriteBlock("CLOSE TRADE (PaperBarSim)",
                "SignalId: " + signalId,
                "Reason: " + reason,
                "PnL (ticks): " + pnlTicks.ToString("F1", CultureInfo.InvariantCulture),
                "BarsInTrade: " + barsInTrade);
        }

        public void PositionChanged(MarketPosition marketPosition, double averagePrice, int quantity)
        {
            if (marketPosition == _lastMarketPosition
                && quantity == _lastQuantity
                && Math.Abs(averagePrice - _lastAveragePrice) < 1e-9)
                return;

            _lastMarketPosition = marketPosition;
            _lastQuantity = quantity;
            _lastAveragePrice = averagePrice;

            WriteBlock("POSITION CHANGED",
                "MarketPosition: " + marketPosition,
                "AveragePrice: " + averagePrice.ToString("F2", CultureInfo.InvariantCulture),
                "Quantity: " + quantity);
        }

        public void OrderCreated(string orderId, OrderState state, string orderAction, int quantity)
        {
            WriteBlock("ORDER CREATED",
                "OrderId: " + orderId,
                "State: " + state,
                "Action: " + orderAction,
                "Quantity: " + quantity);
        }

        public void OrderFilled(string orderId, double fillPrice, DateTime fillTime, int filled)
        {
            WriteBlock("ORDER FILLED",
                "OrderId: " + orderId,
                "FillPrice: " + fillPrice.ToString("F2", CultureInfo.InvariantCulture),
                "FillTime: " + fillTime.ToString("O", CultureInfo.InvariantCulture),
                "Filled: " + filled);
        }

        public void OrderCancelled(string orderId, string reason, OrderState state)
        {
            WriteBlock("ORDER CANCELLED",
                "OrderId: " + orderId,
                "State: " + state,
                "Reason: " + reason);
        }

        void WriteBlock(string eventName, params string[] lines)
        {
            var sb = new StringBuilder();
            sb.AppendLine("==========================");
            sb.AppendLine("ULTRON EXECUTION");
            sb.AppendLine("==========================");
            sb.AppendLine(eventName);
            foreach (string line in lines)
                sb.AppendLine(line);
            sb.AppendLine("==========================");

            string text = sb.ToString();
            _writer?.Write(text);
            _writer?.WriteLine();
            _output?.Invoke(text);
        }
    }
}
