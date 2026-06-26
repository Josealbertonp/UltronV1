using System;

namespace UltronV1.Core.Features
{
    internal struct HistoryBar
    {
        public double High;
        public double Low;
        public double Close;
        public long Volume;
        public double EmaFast;
        public double EmaSlow;
    }

    /// <summary>
    /// Fixed-size ring buffer for structure/chop/wyckoff calculations without per-bar allocation.
    /// </summary>
    internal sealed class BarHistoryBuffer
    {
        readonly HistoryBar[] _bars;
        int _count;
        int _head;

        public BarHistoryBuffer(int capacity = 128)
        {
            _bars = new HistoryBar[capacity];
        }

        public int Count => _count;

        public void Push(double high, double low, double close, long volume, double emaFast, double emaSlow)
        {
            _head = (_head + 1) % _bars.Length;
            _bars[_head] = new HistoryBar
            {
                High = high,
                Low = low,
                Close = close,
                Volume = volume,
                EmaFast = emaFast,
                EmaSlow = emaSlow
            };
            if (_count < _bars.Length)
                _count++;
        }

        public HistoryBar this[int barsAgo]
        {
            get
            {
                if (barsAgo < 0 || barsAgo >= _count)
                    return default;
                int idx = _head - barsAgo;
                if (idx < 0)
                    idx += _bars.Length;
                return _bars[idx];
            }
        }

        public void Reset()
        {
            _count = 0;
            _head = 0;
            Array.Clear(_bars, 0, _bars.Length);
        }
    }
}
