using Microsoft.AspNetCore.Mvc;
using The_Charity.Models;
using The_Charity.Services;
using System.Threading.Tasks;
using The_Charity.Services.Service_Contracts;

namespace The_Charity.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TransfersController : ControllerBase
    {
        private readonly ITransferService _transferService;

        public TransfersController(ITransferService transferService)
        {
            _transferService = transferService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var transfers = await _transferService.GetAllAsync();
            return Ok(transfers);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var transfer = await _transferService.GetByIdAsync(id);
            if (transfer == null)
            {
                return NotFound();
            }
            return Ok(transfer);
        }

        [HttpGet("charity/{charityId}")]
        public async Task<IActionResult> GetByCharityId(Guid charityId)
        {
            var transfers = await _transferService.GetByCharityIdAsync(charityId);
            return Ok(transfers);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] Transfer transfer)
        {
            var createdTransfer = await _transferService.CreateAsync(transfer);
            return CreatedAtAction(nameof(GetById), new { id = createdTransfer.Id }, createdTransfer);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] Transfer transfer)
        {
            if (id != transfer.Id)
            {
                return BadRequest();
            }

            var updatedTransfer = await _transferService.UpdateAsync(transfer);
            return Ok(updatedTransfer);
        }
    }
}
