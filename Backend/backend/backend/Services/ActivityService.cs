using Microsoft.EntityFrameworkCore;
using The_Charity.AppDBContext;
using The_Charity.Models;
using System.Collections.Generic;
using System.Threading.Tasks;
using The_Charity.Services.Service_Contracts;

namespace The_Charity.Services
{
    public class ActivityService : IActivityService
    {
        private readonly AppDbContext _context;

        public ActivityService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Activity> CreateAsync(Activity activity)
        {
            _context.Activities.Add(activity);
            await _context.SaveChangesAsync();
            return activity;
        }

        public async Task<IEnumerable<Activity>> GetAllAsync()
        {
            return await _context.Activities.Include(a => a.Charity).Include(a => a.User).ToListAsync();
        }

        public async Task<IEnumerable<Activity>> GetByCharityIdAsync(Guid charityId)
        {
            return await _context.Activities.Include(a => a.Charity).Include(a => a.User).Where(a => a.CharityId == charityId).ToListAsync();
        }

        public async Task<IEnumerable<Activity>> GetByUserIdAsync(Guid userId)
        {
            return await _context.Activities.Include(a => a.Charity).Include(a => a.User).Where(a => a.UserId == userId).ToListAsync();
        }

        public async Task<Activity> GetByIdAsync(Guid id)
        {
            return await _context.Activities.Include(a => a.Charity).Include(a => a.User).FirstOrDefaultAsync(a => a.Id == id);
        }
    }
}
