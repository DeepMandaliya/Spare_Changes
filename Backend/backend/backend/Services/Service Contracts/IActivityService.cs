using The_Charity.Models;

namespace The_Charity.Services.Service_Contracts
{
    public interface IActivityService
    {
        Task<Activity> CreateAsync(Activity activity);
        Task<Activity> GetByIdAsync(Guid id);
        Task<IEnumerable<Activity>> GetAllAsync();
        Task<IEnumerable<Activity>> GetByCharityIdAsync(Guid charityId);
        Task<IEnumerable<Activity>> GetByUserIdAsync(Guid userId);
    }
}
