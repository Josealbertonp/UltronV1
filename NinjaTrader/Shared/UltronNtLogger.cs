using System;
using UltronV1.Core.Engine;

namespace NinjaTrader.NinjaScript.Ultron
{
    /// <summary>Shared IUltronLogger for Indicator and Strategy hosts.</summary>
    public sealed class UltronNtLogger : IUltronLogger
    {
        readonly Action<string> _print;

        public UltronNtLogger(Action<string> print)
        {
            _print = print ?? throw new ArgumentNullException(nameof(print));
        }

        public void Info(string message) => _print(message);
        public void Warn(string message) => _print(message);
        public void Error(string message) => _print(message);
    }
}
