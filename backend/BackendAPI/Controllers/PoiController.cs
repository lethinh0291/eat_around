using SharedLib.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BackendAPI.Data;


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
    //1.
    [HttpGet]
    public async Task<ActionResult<IEnumerable<POI>>> GetAll()
    {
        return await _context.POIs.ToListAsync();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<POI>> GetById(int id)
    {
        var poi = await _context.POIs.FindAsync(id);
        if (poi == null) return NotFound(new { Message = $"POI với mã ID {id} không tồn tại." });
        return poi;
    }
    //2Post
    [HttpPost]
    public async Task<ActionResult<POI>> Create(POI poi)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        _context.POIs.Add(poi);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = poi.Id }, poi);
    }

    //3.
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, POI poi)
    {
        if (id != poi.Id) return BadRequest(new { Message = "ID trong URL không khớp" });
        _context.Entry(poi).State = EntityState.Modified;
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!_context.POIs.Any(e => e.Id == id))
                return NotFound(new { Message = $"POI với mã ID {id} không tồn tại." });
            else throw;
        }
        return NoContent();
    }

    //4.
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var poi = await _context.POIs.FindAsync(id);
        if (poi == null) return NotFound(new { Message = $"POI với mã ID {id} không tồn tại." });

        _context.POIs.Remove(poi);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}