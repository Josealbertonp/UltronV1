using System.Collections.Generic;
using UltronV1.Core.Engine;

namespace UltronV1.Tests.Support
{
    public sealed class ListUltronLogger : IUltronLogger
    {
        public List<string> InfoMessages { get; } = new List<string>();
        public List<string> WarnMessages { get; } = new List<string>();
        public List<string> ErrorMessages { get; } = new List<string>();

        public void Info(string message) => InfoMessages.Add(message);
        public void Warn(string message) => WarnMessages.Add(message);
        public void Error(string message) => ErrorMessages.Add(message);
    }
}
