using Microsoft.AspNetCore.Mvc;
using The_Charity.Models;
using The_Charity.Services;
using System.Threading.Tasks;
using The_Charity.Services.Service_Contracts;

namespace The_Charity.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ActivitiesController : ControllerBase
    {
        private readonly IActivityService _activityService;

        public ActivitiesController(IActivityService activityService)
        {
            _activityService = activityService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var activities = await _activityService.GetAllAsync();
            return Ok(activities);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var activity = await _activityService.GetByIdAsync(id);
            if (activity == null)
            {
                return NotFound();
            }
            return Ok(activity);
        }

        [HttpGet("charity/{charityId}")]
        public async Task<IActionResult> GetByCharityId(Guid charityId)
        {
            var activities = await _activityService.GetByCharityIdAsync(charityId);
            return Ok(activities);
        }

        [HttpGet("user/{userId}")]
        public async Task<IActionResult> GetByUserId(Guid userId)
        {
            var activities = await _activityService.GetByUserIdAsync(userId);
            return Ok(activities);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] Activity activity)
        {
            var createdActivity = await _activityService.CreateAsync(activity);
            return CreatedAtAction(nameof(GetById), new { id = createdActivity.Id }, createdActivity);
        }
    }
}
