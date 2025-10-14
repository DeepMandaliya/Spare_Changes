using Microsoft.EntityFrameworkCore;
using Stripe;
using System.Text.Json;
using The_Charity.AppDBContext;
using The_Charity.Models;
using The_Charity.Services.Service_Contracts;

namespace The_Charity.Services
{
    public class TransactionService : ITransactionService
    {
        private readonly AppDbContext _db;
        private readonly PlaidServices _plaidService;
        private readonly ILogger<TransactionService> _logger;

        public TransactionService(AppDbContext db, PlaidServices plaidService, ILogger<TransactionService> logger)
        {
            _db = db;
            _plaidService = plaidService;
            _logger = logger;
        }
        public async Task ProcessRoundUpForUser(Guid userId)
        {
            var plaidItem = await _db.PlaidItems
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (plaidItem == null)
            {
                _logger.LogWarning("No Plaid item found for user {UserId}", userId);
                return;
            }

            var userPreferences = await _db.UserPreferences
                .Include(up => up.DefaultCharity)
                .FirstOrDefaultAsync(up => up.UserId == userId);

            if (userPreferences?.AutoRoundUp != true)
            {
                _logger.LogInformation("Auto round-up disabled for user {UserId}", userId);
                return;
            }

            // Get transactions from last 24 hours
            var endDate = DateTime.UtcNow;
            var startDate = endDate.AddDays(-1);

            var transactionsRes = await _plaidService.GetTransactionsAsync(plaidItem.AccessToken, startDate, endDate);
            if (!transactionsRes.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to get transactions for user {UserId}", userId);
                return;
            }

            var transactionsJson = await transactionsRes.Content.ReadAsStringAsync();
            var transactionsData = JsonSerializer.Deserialize<JsonElement>(transactionsJson);

            if (transactionsData.TryGetProperty("transactions", out var transactionsElement))
            {
                foreach (var transaction in transactionsElement.EnumerateArray())
                {
                    await ProcessTransaction(transaction, userId, userPreferences);
                }
            }

            plaidItem.LastSynced = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        private async Task ProcessTransaction(JsonElement transaction, Guid userId, UserPrefernce preferences)
        {
            var transactionId = transaction.GetProperty("transaction_id").GetString();
            var amount = transaction.GetProperty("amount").GetDecimal();
            var name = transaction.GetProperty("name").GetString();
            var date = transaction.GetProperty("date").GetString();

            // Skip if already processed
            var existing = await _db.Transactions
                .AnyAsync(t => t.PlaidTransactionId == transactionId);
            if (existing) return;

            // Calculate round-up
            var roundUpAmount = await CalculateRoundUp(amount);

            // Check threshold
            if (roundUpAmount < preferences.RoundUpThreshold)
            {
                _logger.LogInformation("Round-up amount {Amount} below threshold for user {UserId}", roundUpAmount, userId);
                return;
            }

            // Check monthly limit
            var monthlyDonations = await GetMonthlyDonations(userId);
            if (monthlyDonations + roundUpAmount > preferences.MonthlyDonationLimit)
            {
                _logger.LogInformation("Monthly donation limit reached for user {UserId}", userId);
                return;
            }

            // Create transaction record
            var donationTransaction = new Transaction
            {
                UserId = userId,
                CharityId = preferences.DefaultCharityId,
                PlaidTransactionId = transactionId,
                OriginalAmount = amount,
                RoundUpAmount = roundUpAmount,
                TotalAmount = roundUpAmount,
                Status = "pending",
                Description = $"Round-up from {name}",
                TransactionDate = DateTime.Parse(date)
            };

            _db.Transactions.Add(donationTransaction);
            await _db.SaveChangesAsync();

            _logger.LogInformation("Created round-up transaction {TransactionId} for user {UserId}", donationTransaction.Id, userId);
        }

        public async Task<decimal> CalculateRoundUp(decimal amount)
        {
            // Round up to nearest dollar
            return Math.Ceiling(amount) - amount;
        }

        public async Task<List<Transaction>> GetUserTransactions(Guid userId)
        {
            return await _db.Transactions
                .Include(t => t.Charity)
                .Where(t => t.UserId == userId)
                .OrderByDescending(t => t.TransactionDate)
                .ToListAsync();
        }

        public async Task<decimal> GetTotalDonations(Guid userId)
        {
            return await _db.Transactions
                .Where(t => t.UserId == userId && t.Status == "completed")
                .SumAsync(t => t.RoundUpAmount);
        }

        private async Task<decimal> GetMonthlyDonations(Guid userId)
        {
            var startOfMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
            return await _db.Transactions
                .Where(t => t.UserId == userId &&
                           t.Status == "completed" &&
                           t.CreatedAt >= startOfMonth)
                .SumAsync(t => t.RoundUpAmount);
        }
    }

}

