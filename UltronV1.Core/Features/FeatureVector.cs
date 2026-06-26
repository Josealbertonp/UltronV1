using System;

namespace UltronV1.Core.Features
{
    /// <summary>
    /// Reusable feature container. One instance per engine — overwritten each bar (zero alloc steady state).
    /// </summary>
    public sealed class FeatureVector
    {
        readonly float[] _values = new float[FeatureSchema.Count];

        public float[] Values => _values;

        public float this[int index] => _values[index];

        public void Clear()
        {
            Array.Clear(_values, 0, _values.Length);
        }

        public FeatureVector Clone()
        {
            var copy = new FeatureVector();
            Array.Copy(_values, copy._values, _values.Length);
            return copy;
        }
    }
}
