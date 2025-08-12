using Microsoft.AspNetCore.Mvc;
using MyWeb.Core.Communication;
using System.Text.Json;

namespace MyWeb.WebApp.Controllers
{
    /// <summary>
    /// PLC okuma/yazma ve sağlık/kalite testleri için basit REST API.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class PlcController : ControllerBase
    {
        private readonly ICommunicationChannel _channel;

        public PlcController(ICommunicationChannel channel)
        {
            _channel = channel;
        }

        // ---- Basit okuma: mevcut testlerinle uyumlu ----
        // GET /api/plc/read?tagName=...
        [HttpGet("read")]
        public IActionResult Read([FromQuery] string tagName)
        {
            if (string.IsNullOrWhiteSpace(tagName))
                return BadRequest("tagName boş olamaz.");

            var val = _channel.ReadTag<object>(tagName);
            return Ok(new { tag = tagName, value = val });
        }

        // ---- Basit yazma: mevcut testlerinle uyumlu ----
        // POST /api/plc/write
        // { "tagName":"tBool", "value": true }
        public class WriteRequest
        {
            public string TagName { get; set; } = string.Empty;
            public object? Value { get; set; }
        }

        [HttpPost("write")]
        public IActionResult Write([FromBody] WriteRequest req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.TagName))
                return BadRequest("Geçersiz istek. TagName zorunlu.");

            bool ok = _channel.WriteTag(req.TagName, req.Value ?? JsonDocument.Parse("null").RootElement);
            if (ok) return Ok($"Tag '{req.TagName}' değer yazıldı.");
            return StatusCode(500, $"Tag '{req.TagName}' yazılamadı.");
        }

        // ---- Yeni: Sağlık bilgisi ----
        // GET /api/plc/health
        [HttpGet("health")]
        public IActionResult Health()
        {
            var h = _channel.GetHealth();
            return Ok(h);
        }

        // ---- Yeni: Hata atmadan okuma denemesi ----
        // GET /api/plc/try-read?tagName=...
        [HttpGet("try-read")]
        public IActionResult TryRead([FromQuery] string tagName)
        {
            if (string.IsNullOrWhiteSpace(tagName))
                return BadRequest("tagName boş olamaz.");

            if (_channel.TryReadTag<object>(tagName, out var value, out var error))
                return Ok(new { tag = tagName, value });

            return StatusCode(500, new { tag = tagName, error });
        }

        // ---- Yeni: Kalite + timestamp ile toplu okuma ----
        // GET /api/plc/read-many?names=tBool,tInt,tLReal
        [HttpGet("read-many")]
        public IActionResult ReadMany([FromQuery] string names)
        {
            if (string.IsNullOrWhiteSpace(names))
                return BadRequest("names boş olamaz. Örn: tBool,tInt,tLReal");

            var list = names.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            var result = _channel.ReadTagsWithQuality(list);
            return Ok(result);
        }
    }
}
