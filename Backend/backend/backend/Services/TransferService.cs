using Microsoft.EntityFrameworkCore;
using The_Charity.AppDBContext;
using The_Charity.Models;
using System.Collections.Generic;
using System.Threading.Tasks;
using The_Charity.Services.Service_Contracts;

namespace The_Charity.Services
{
    public class TransferService : ITransferService
    {
        private readonly AppDbContext _context;

        public TransferService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Transfer> CreateAsync(Transfer transfer)
        {
            _context.Transfers.Add(transfer);
            await _context.SaveChangesAsync();
            return transfer;
        }

        public async Task<IEnumerable<Transfer>> GetAllAsync()
        {
            return await _context.Transfers.Include(t => t.Charity).ToListAsync();
        }

        public async Task<IEnumerable<Transfer>> GetByCharityIdAsync(Guid charityId)
        {
            return await _context.Transfers.Include(t => t.Charity).Where(t => t.CharityId == charityId).ToListAsync();
        }

        public async Task<Transfer> GetByIdAsync(Guid id)
        {
            return await _context.Transfers.Include(t => t.Charity).FirstOrDefaultAsync(t => t.Id == id);
        }

        public async Task<Transfer> UpdateAsync(Transfer transfer)
        {
            _context.Entry(transfer).State = EntityState.Modified;
            await _context.SaveChangesAsync();
            return transfer;
        }
    }
}
