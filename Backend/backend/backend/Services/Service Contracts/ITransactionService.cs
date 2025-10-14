using The_Charity.Models;

namespace The_Charity.Services.Service_Contracts
{
    public interface ITransactionService
    {
        Task ProcessRoundUpForUser(Guid userId);
        Task<decimal> CalculateRoundUp(decimal amount);
        Task<List<Transaction>> GetUserTransactions(Guid userId);
        Task<decimal> GetTotalDonations(Guid userId);
    }
}
