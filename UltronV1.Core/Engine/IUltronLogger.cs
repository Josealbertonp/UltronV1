namespace UltronV1.Core.Engine
{
    /// <summary>
    /// Abstraction for host logging (NT8 Print, tests, file logger).
    /// </summary>
    public interface IUltronLogger
    {
        void Info(string message);
        void Warn(string message);
        void Error(string message);
    }

    public sealed class NullUltronLogger : IUltronLogger
    {
        public static readonly NullUltronLogger Instance = new NullUltronLogger();

        NullUltronLogger() { }

        public void Info(string message) { }
        public void Warn(string message) { }
        public void Error(string message) { }
    }
}
