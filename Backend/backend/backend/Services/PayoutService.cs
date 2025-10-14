using Microsoft.EntityFrameworkCore;
using The_Charity.AppDBContext;
using The_Charity.Models;
using System.Collections.Generic;
using System.Threading.Tasks;
using The_Charity.Services.Service_Contracts;

namespace The_Charity.Services
{
    public class PayoutService : IPayoutService
    {
        private readonly AppDbContext _context;

        public PayoutService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Payout> CreateAsync(Payout payout)
        {
            _context.Payouts.Add(payout);
            await _context.SaveChangesAsync();
            return payout;
        }

        public async Task<IEnumerable<Payout>> GetAllAsync()
        {
            return await _context.Payouts.Include(p => p.Charity).ToListAsync();
        }

        public async Task<IEnumerable<Payout>> GetByCharityIdAsync(Guid charityId)
        {
            return await _context.Payouts.Include(p => p.Charity).Where(p => p.CharityId == charityId).ToListAsync();
        }

        public async Task<Payout> GetByIdAsync(Guid id)
        {
            return await _context.Payouts.Include(p => p.Charity).FirstOrDefaultAsync(p => p.Id == id);
        }

        public async Task<Payout> UpdateAsync(Payout payout)
        {
            _context.Entry(payout).State = EntityState.Modified;
            await _context.SaveChangesAsync();
            return payout;
        }
    }
}
