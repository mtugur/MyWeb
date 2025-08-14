using Microsoft.AspNetCore.Mvc;
using MyWeb.Core.Communication;

namespace MyWeb.WebApp.Controllers.Api
{
    [ApiController]
    [Route("api/[controller]")]
    public class DiagnosticsController : ControllerBase
    {
        private readonly ICommunicationChannel _channel;

        public DiagnosticsController(ICommunicationChannel channel)
        {
            _channel = channel;
        }

        // Basit ping
        [HttpGet("ping")]
        public IActionResult Ping()
        {
            return Ok(new { connected = _channel.IsConnected });
        }

        // Bağlanmayı zorla ve sonucu döndür
        [HttpPost("connect")]
        public IActionResult Connect()
        {
            var ok = _channel.Connect();
            var health = _channel.GetHealth();
            return Ok(new
            {
                ok,
                health.IsConnected,
                health.LastOkUtc,
                health.LastErrorMessage,
                health.ReconnectCount,
                health.LastReconnectUtc
            });
        }

        // Ayrıntılı sağlık bilgisi
        [HttpGet("health")]
        public IActionResult Health()
        {
            var h = _channel.GetHealth();
            return Ok(new
            {
                h.IsConnected,
                h.StartTimeUtc,
                h.LastOkUtc,
                h.LastErrorMessage,
                h.ReconnectCount,
                h.LastReconnectUtc,
                h.UptimeSeconds
            });
        }
    }
}
