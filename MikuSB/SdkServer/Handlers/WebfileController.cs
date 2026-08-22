using Microsoft.AspNetCore.Mvc;

namespace MikuSB.SdkServer.Handlers;

[ApiController]
public class WebfileController : ControllerBase
{
    [HttpGet("/ob202307/webfile/{**path}")]
    [HttpPost("/ob202307/webfile/{**path}")]
    public IActionResult Webfile(string path)
    {
        var file = (path ?? "").Replace('\\', '/').Trim('/');
        if (file.EndsWith("banner/config/gm-gm.json", StringComparison.OrdinalIgnoreCase) ||
            file.EndsWith("announce/config/gm-gm.json", StringComparison.OrdinalIgnoreCase))
        {
            return Ok(new
            {
                ret = 0,
                code = 0,
                msg = "ok",
                data = Array.Empty<object>()
            });
        }

        if (file.EndsWith("jump/config/jumpconfig.json", StringComparison.OrdinalIgnoreCase))
        {
            return Ok(new
            {
                ret = 0,
                code = 0,
                msg = "ok",
                data = new { jump = Array.Empty<object>() }
            });
        }

        return Ok(new
        {
            ret = 0,
            code = 0,
            msg = "ok",
            data = new object()
        });
    }

    [HttpGet("/{hash}/PC/updates/version_require.json")]
    [HttpPost("/{hash}/PC/updates/version_require.json")]
    public IActionResult VersionRequire(string hash)
    {
        return Ok(new
        {
            ret = 0,
            code = 0,
            msg = "ok",
            need_update = false,
            force_update = 0,
            files = Array.Empty<object>(),
            data = new
            {
                need_update = false,
                force_update = 0,
                files = Array.Empty<object>()
            }
        });
    }
}
