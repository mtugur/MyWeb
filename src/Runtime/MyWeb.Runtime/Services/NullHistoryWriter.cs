using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using MyWeb.Core.History;

namespace MyWeb.Runtime.Services
{
    /// <summary>
    /// V2 mimarisinde HistoryWriter dışarıdan enqueue beklemez.
    /// TagSamplingService'in DI bağımlılığını kırmak için no-op adaptör.
    /// </summary>
    public sealed class NullHistoryWriter : IHistoryWriter
    {
        private readonly ILogger<NullHistoryWriter> _log;
        private bool _warned;

        public NullHistoryWriter(ILogger<NullHistoryWriter> log)
        {
            _log = log;
        }

        public void Enqueue(SamplePoint item)
        {
            WarnOnce();
            // no-op
        }

        public void Enqueue(IEnumerable<SamplePoint> items)
        {
            WarnOnce();
            // no-op
        }

        private void WarnOnce()
        {
            if (_warned) return;
            _warned = true;
            _log.LogDebug("NullHistoryWriter aktif: V2 writer dışarıdan enqueue kabul etmiyor (no-op).");
        }
    }
}
