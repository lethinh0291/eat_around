using BackendAPI.Data;
using BackendAPI.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BackendAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PoiController : ControllerBase
{
    private readonly AppDbContext _context;

    public PoiController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<POI>>> GetAll()
    {
        return await _context.POIs.ToListAsync();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<POI>> GetById(int id)
    {
        var poi = await _context.POIs.FindAsync(id);
        if (poi == null) return NotFound(new { Message = $"POI with ID {id} not found." });
        return poi;
    }
    //Post
    [HttpPost]
    public async Task<ActionResult<POI>> Create(POI poi)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        _context.POIs.Add(poi);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = poi.Id }, poi);
    }
}