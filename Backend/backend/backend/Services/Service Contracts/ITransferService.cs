using The_Charity.Models;

namespace The_Charity.Services.Service_Contracts
{
    public interface ITransferService
    {
        Task<Transfer> CreateAsync(Transfer transfer);
        Task<Transfer> GetByIdAsync(Guid id);
        Task<IEnumerable<Transfer>> GetAllAsync();
        Task<IEnumerable<Transfer>> GetByCharityIdAsync(Guid charityId);
        Task<Transfer> UpdateAsync(Transfer transfer);
    }
}
