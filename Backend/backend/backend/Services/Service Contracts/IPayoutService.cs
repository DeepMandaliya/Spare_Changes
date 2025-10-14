using The_Charity.Models;

namespace The_Charity.Services.Service_Contracts
{
    public interface IPayoutService
    {
        Task<Payout> CreateAsync(Payout payout);
        Task<Payout> GetByIdAsync(Guid id);
        Task<IEnumerable<Payout>> GetAllAsync();
        Task<IEnumerable<Payout>> GetByCharityIdAsync(Guid charityId);
        Task<Payout> UpdateAsync(Payout payout);
    }
}
