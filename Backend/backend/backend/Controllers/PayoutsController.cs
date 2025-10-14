using Microsoft.AspNetCore.Mvc;
using The_Charity.Models;
using The_Charity.Services;
using System.Threading.Tasks;
using The_Charity.Services.Service_Contracts;

namespace The_Charity.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PayoutsController : ControllerBase
    {
        private readonly IPayoutService _payoutService;

        public PayoutsController(IPayoutService payoutService)
        {
            _payoutService = payoutService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var payouts = await _payoutService.GetAllAsync();
            return Ok(payouts);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var payout = await _payoutService.GetByIdAsync(id);
            if (payout == null)
            {
                return NotFound();
            }
            return Ok(payout);
        }

        [HttpGet("charity/{charityId}")]
        public async Task<IActionResult> GetByCharityId(Guid charityId)
        {
            var payouts = await _payoutService.GetByCharityIdAsync(charityId);
            return Ok(payouts);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] Payout payout)
        {
            var createdPayout = await _payoutService.CreateAsync(payout);
            return CreatedAtAction(nameof(GetById), new { id = createdPayout.Id }, createdPayout);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] Payout payout)
        {
            if (id != payout.Id)
            {
                return BadRequest();
            }

            var updatedPayout = await _payoutService.UpdateAsync(payout);
            return Ok(updatedPayout);
        }
    }
}
