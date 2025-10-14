using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using The_Charity.AppDBContext;
using The_Charity.Models;
using The_Charity.Models.DTOs;

namespace The_Charity.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CharitiesController : ControllerBase
    {
        private readonly AppDbContext _db;

        public CharitiesController(AppDbContext db)
        {
            _db = db;
        }

        [HttpGet]
        public async Task<IActionResult> GetCharities()
        {
            var charities = await _db.Charities
                .Where(c => c.IsActive)
                .OrderBy(c => c.Name)
                .ToListAsync();

            return Ok(charities.Select(c => new
            {
                id = c.Id,
                name = c.Name,
                description = c.Description,
                logoUrl = c.LogoUrl,
                website = c.Website
            }));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetCharity(Guid id)
        {
            var charity = await _db.Charities
                .FirstOrDefaultAsync(c => c.Id == id && c.IsActive);

            if (charity == null) return NotFound();

            return Ok(new
            {
                id = charity.Id,
                name = charity.Name,
                description = charity.Description,
                logoUrl = charity.LogoUrl,
                website = charity.Website
            });
        }

        [HttpPost]
        public async Task<IActionResult> CreateCharity([FromBody] CreateCharityRequest request)
        {
            var charity = new Charity
            {
                Name = request.Name,
                Description = request.Description,
                LogoUrl = request.LogoUrl,
                Website = request.Website,
                StripeAccountId = request.StripeAccountId,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _db.Charities.Add(charity);
            await _db.SaveChangesAsync();

            return Ok(new
            {
                id = charity.Id,
                name = charity.Name,
                message = "Charity created successfully"
            });
        }
    }
}
