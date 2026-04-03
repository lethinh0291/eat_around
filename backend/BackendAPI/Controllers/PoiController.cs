using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
public class PoiController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok("API OK");
    }
}