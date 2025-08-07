using Microsoft.AspNetCore.Mvc;
using MyWeb.Core.Communication;
using System.Collections.Generic;

namespace MyWeb.WebApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PlcController : ControllerBase
    {
        private readonly ICommunicationChannel _channel;

        public PlcController(ICommunicationChannel channel)
        {
            _channel = channel;
        }

        [HttpGet("read")]
        public IActionResult Read([FromQuery] string tagName)
        {
            if (string.IsNullOrWhiteSpace(tagName))
                return BadRequest("tagName boş olamaz.");

            try
            {
                var value = _channel.ReadTag<object>(tagName);
                return Ok(new { Tag = tagName, Value = value });
            }
            catch (KeyNotFoundException)
            {
                return NotFound($"Tag '{tagName}' bulunamadı.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Okuma hatası: {ex.Message}");
            }
        }

        public class WriteRequest
        {
            public string TagName { get; set; } = string.Empty;
            public required object Value { get; set; }
        }

        [HttpPost("write")]
        public IActionResult Write([FromBody] WriteRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.TagName))
                return BadRequest("TagName boş olamaz.");

            try
            {
                bool ok = _channel.WriteTag(req.TagName, req.Value);
                if (ok)
                    return Ok($"Tag '{req.TagName}' değer yazıldı.");
                else
                    return StatusCode(500, $"Tag '{req.TagName}' yazılamadı.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Yazma hatası: {ex.Message}");
            }
        }
    }
}
