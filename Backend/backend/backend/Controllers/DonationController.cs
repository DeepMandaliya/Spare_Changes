using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stripe;
using System.Text.Json;
using The_Charity.AppDBContext;
using The_Charity.Models;
using The_Charity.Models.DTOs;
using The_Charity.Services;
using The_Charity.Services.Service_Contracts;

namespace The_Charity.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DonationController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly StripeService _stripeService;
        private readonly ITransactionService _transactionService;
        private readonly ILogger<DonationController> _logger;
        private readonly PlaidServices _plaid;
        private readonly IConfiguration _configuration;

        public DonationController(
            AppDbContext db,
            StripeService stripeService,
            ITransactionService transactionService,
            IConfiguration configuration,
            PlaidServices plaidServices, 
            ILogger<DonationController> logger)
        {
            _db = db;
            _stripeService = stripeService;
            _transactionService = transactionService;
            _configuration = configuration;
            _logger = logger;
            _plaid = plaidServices;
        }

        // Add these new endpoints to DonationController.cs

        [HttpGet("round-up-opportunities/{userId}")]
        public async Task<IActionResult> GetRoundUpOpportunities(Guid userId)
        {
            try
            {
                var user = await _db.Users.FindAsync(userId);
                if (user == null) return NotFound("User not found");

                // Get user's Plaid items to fetch transactions
                var plaidItems = await _db.PlaidItems
                    .Where(p => p.UserId == userId)
                    .ToListAsync();

                var roundUpOpportunities = new List<object>();
                decimal totalRoundUpAmount = 0;

                // For each Plaid item, get recent transactions and calculate round-ups
                foreach (var plaidItem in plaidItems)
                {
                    try
                    {
                        // Get recent transactions from Plaid
                        var transactionsRes = await _plaid.GetTransactionsAsync(
                            plaidItem.AccessToken,
                            DateTime.UtcNow.AddDays(-30), // Last 30 days
                            DateTime.UtcNow
                        );

                        if (transactionsRes.IsSuccessStatusCode)
                        {
                            var transactionsJson = await transactionsRes.Content.ReadAsStringAsync();
                            var transactionsDoc = JsonDocument.Parse(transactionsJson);

                            if (transactionsDoc.RootElement.TryGetProperty("transactions", out var transactions))
                            {
                                foreach (var transaction in transactions.EnumerateArray())
                                {
                                    // Only process debit/outflow transactions
                                    if (transaction.TryGetProperty("amount", out var amountElement) &&
                                        decimal.TryParse(amountElement.GetString(), out var amount) &&
                                        amount > 0)
                                    {
                                        var roundUpAmount = CalculateRoundUp(amount);

                                        if (roundUpAmount > 0)
                                        {
                                            roundUpOpportunities.Add(new
                                            {
                                                transactionId = transaction.GetProperty("transaction_id").GetString(),
                                                name = transaction.GetProperty("name").GetString(),
                                                amount = amount,
                                                roundUpAmount = roundUpAmount,
                                                date = transaction.GetProperty("date").GetString(),
                                                category = transaction.TryGetProperty("category", out var cat) ?
                                                          string.Join(", ", cat.EnumerateArray().Select(c => c.GetString())) : "Other"
                                            });

                                            totalRoundUpAmount += roundUpAmount;
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to get transactions for Plaid item {ItemId}", plaidItem.ItemId);
                    }
                }

                return Ok(new
                {
                    opportunities = roundUpOpportunities,
                    totalRoundUpAmount = Math.Round(totalRoundUpAmount, 2),
                    opportunityCount = roundUpOpportunities.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting round-up opportunities for user {UserId}", userId);
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost("process-roundup-donation")]
        public async Task<IActionResult> ProcessRoundupDonation([FromBody] RoundUpDonationRequest request)
        {
            try
            {
                var user = await _db.Users
                    .Include(u => u.PaymentMethods)
                    .FirstOrDefaultAsync(u => u.Id == request.UserId);

                if (user == null) return NotFound("User not found");

                var defaultPaymentMethod = user.PaymentMethods
                    .FirstOrDefault(p => p.IsDefault && p.IsActive);

                if (defaultPaymentMethod == null)
                    return BadRequest(new { error = "No default payment method found" });

                // Validate amount
                if (request.Amount <= 0)
                    return BadRequest(new { error = "Amount must be greater than 0" });

                // Process donation
                var donation = new Donation
                {
                    UserId = request.UserId,
                    CharityId = request.CharityId,
                    Amount = request.Amount,
                    PaymentMethodId = defaultPaymentMethod.Id,
                    Type = "roundup",
                    Status = "pending",
                    CreatedAt = DateTime.UtcNow
                };

                _db.Donations.Add(donation);
                await _db.SaveChangesAsync();

                // Process payment
                try
                {
                    var paymentIntentService = new PaymentIntentService();
                    var paymentIntent = await paymentIntentService.CreateAsync(new PaymentIntentCreateOptions
                    {
                        Amount = (long)(request.Amount * 100),
                        Currency = "usd",
                        Customer = user.StripeCustomerId,
                        PaymentMethod = defaultPaymentMethod.StripePaymentMethodId,
                        Confirm = true,
                        Metadata = new Dictionary<string, string>
                {
                    { "donation_id", donation.Id.ToString() },
                    { "user_id", user.Id.ToString() },
                    { "charity_id", request.CharityId.ToString() },
                    { "type", "roundup" }
                }
                    });

                    if (paymentIntent.Status == "succeeded")
                    {
                        donation.Status = "completed";
                        donation.CompletedAt = DateTime.UtcNow;
                        await _db.SaveChangesAsync();

                        // Create transaction record
                        var charity = await _db.Charities.FindAsync(request.CharityId);
                        var transaction = new Transaction
                        {
                            UserId = request.UserId,
                            CharityId = request.CharityId,
                            TotalAmount = request.Amount,
                            OriginalAmount = request.Amount,
                            RoundUpAmount = 0,
                            Status = "completed",
                            Description = $"Round-up donation to {charity?.Name}",
                            TransactionDate = DateTime.UtcNow,
                            CreatedAt = DateTime.UtcNow,
                            ProcessedAt = DateTime.UtcNow,
                            StripePaymentIntentId = paymentIntent.Id
                        };

                        _db.Transactions.Add(transaction);
                        await _db.SaveChangesAsync();

                        return Ok(new
                        {
                            message = "Round-up donation completed successfully",
                            donationId = donation.Id,
                            transactionId = transaction.Id,
                            amount = donation.Amount,
                            charityName = charity?.Name,
                            paymentIntentId = paymentIntent.Id
                        });
                    }
                    else
                    {
                        donation.Status = "failed";
                        await _db.SaveChangesAsync();
                        return BadRequest(new { error = "Payment failed", details = paymentIntent.Status });
                    }
                }
                catch (StripeException ex)
                {
                    donation.Status = "failed";
                    await _db.SaveChangesAsync();
                    return BadRequest(new { error = "Payment failed", details = ex.Message });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing round-up donation for user {UserId}", request.UserId);
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost("setup-auto-donation")]
        public async Task<IActionResult> SetupAutoDonation([FromBody] AutoDonationRequest request)
        {
            try
            {
                var user = await _db.Users.FindAsync(request.UserId);
                if (user == null) return NotFound("User not found");

                // Update or create donation preferences for auto-donation
                var preferences = await _db.DonationPreferences
                    .FirstOrDefaultAsync(p => p.UserId == request.UserId);

                if (preferences == null)
                {
                    preferences = new DonationPreferences
                    {
                        UserId = request.UserId,
                        AutoRoundUp = request.EnableAutoDonation,
                        RoundUpThreshold = request.RoundUpThreshold,
                        MonthlyDonationLimit = request.MonthlyLimit,
                        DonationDayOfMonth = request.DonationDay,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _db.DonationPreferences.Add(preferences);
                }
                else
                {
                    preferences.AutoRoundUp = request.EnableAutoDonation;
                    preferences.RoundUpThreshold = request.RoundUpThreshold;
                    preferences.MonthlyDonationLimit = request.MonthlyLimit;
                    preferences.DonationDayOfMonth = request.DonationDay;
                    preferences.UpdatedAt = DateTime.UtcNow;
                }

                await _db.SaveChangesAsync();

                return Ok(new
                {
                    message = "Auto-donation settings updated successfully",
                    settings = new
                    {
                        preferences.AutoRoundUp,
                        preferences.RoundUpThreshold,
                        preferences.MonthlyDonationLimit,
                        preferences.DonationDayOfMonth
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting up auto-donation for user {UserId}", request.UserId);
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // Helper method to calculate round-up amount
        private decimal CalculateRoundUp(decimal amount)
        {
            var roundedAmount = Math.Ceiling(amount);
            var roundUp = roundedAmount - amount;
            return Math.Round(roundUp, 2);
        }

        [HttpPost("make-direct-donation")]
        public async Task<IActionResult> MakeDirectDonation([FromBody] DirectDonationRequest request)
        {
            try
            {
                _logger.LogInformation("Starting direct donation for user {UserId} with payment method {PaymentMethodId}",
                    request.UserId, request.PaymentMethodId);

                // Check if we're in sandbox mode
                bool isSandbox = _configuration["Plaid:Environment"] == "sandbox";

                // Validate user with payment methods
                var user = await _db.Users
                    .Include(u => u.PaymentMethods)
                    .FirstOrDefaultAsync(u => u.Id == request.UserId);

                if (user == null)
                {
                    return BadRequest(new { error = "User not found" });
                }

                // Find the SPECIFIC payment method by ID from request
                var paymentMethod = user.PaymentMethods.FirstOrDefault(p => p.Id == request.PaymentMethodId);

                if (paymentMethod == null)
                {
                    return BadRequest(new
                    {
                        error = "Payment method not found",
                        details = "The specified payment method does not exist for this user"
                    });
                }

                _logger.LogInformation("Using payment method: {Type} ****{Last4Digit} (ID: {PaymentMethodId})",
                    paymentMethod.Type, paymentMethod.Last4Digit, paymentMethod.Id);

                // Validate charity and amount
                var charity = await _db.Charities.FindAsync(request.CharityId);
                if (charity == null) return BadRequest(new { error = "Charity not found" });
                if (request.Amount <= 0) return BadRequest(new { error = "Amount must be greater than 0" });

                // Create donation record
                var donation = new Donation
                {
                    UserId = request.UserId,
                    CharityId = request.CharityId,
                    Amount = request.Amount,
                    PaymentMethodId = paymentMethod.Id,
                    Type = "direct",
                    Status = "pending",
                    CreatedAt = DateTime.UtcNow
                };

                _db.Donations.Add(donation);
                await _db.SaveChangesAsync();

                // Process payment based on payment method type
                if (paymentMethod.Type.ToLower() == "us_bank_account")
                {
                    return await ProcessBankAccountPayment(user, paymentMethod, charity, donation, request.Amount, isSandbox);
                }
                else
                {
                    return await ProcessCardPayment(user, paymentMethod, charity, donation, request.Amount, isSandbox);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing donation for user {UserId}", request.UserId);
                return StatusCode(500, new
                {
                    success = false,
                    error = "Internal server error processing donation"
                });
            }
        }

        private async Task<IActionResult> ProcessCardPayment(User user, Models.PaymentMethod paymentMethod,
            Charity charity, Donation donation, decimal amount, bool isSandbox)
        {
            try
            {
                _logger.LogInformation("Processing card payment for donation {DonationId}", donation.Id);

                var paymentIntentService = new PaymentIntentService();

                var options = new PaymentIntentCreateOptions
                {
                    Amount = (long)(amount * 100),
                    Currency = "usd",
                    Customer = user.StripeCustomerId,
                    PaymentMethod = paymentMethod.StripePaymentMethodId,
                    Confirm = true,
                    PaymentMethodTypes = new List<string> { "card" },
                    Metadata = new Dictionary<string, string>
            {
                { "donation_id", donation.Id.ToString() },
                { "user_id", user.Id.ToString() },
                { "charity_id", charity.Id.ToString() },
                { "payment_method_id", paymentMethod.Id.ToString() },
                { "payment_type", "card" },
                { "environment", isSandbox ? "sandbox" : "production" }
            }
                };

                var paymentIntent = await paymentIntentService.CreateAsync(options);

                _logger.LogInformation("Card PaymentIntent created with status: {Status}", paymentIntent.Status);

                return await HandlePaymentIntentResult(paymentIntent, user, paymentMethod, charity, donation, amount, isSandbox);
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "Stripe card payment error for donation {DonationId}", donation.Id);

                donation.Status = "failed";
                await _db.SaveChangesAsync();

                return BadRequest(new
                {
                    success = false,
                    error = "Card payment failed",
                    details = ex.StripeError?.Message ?? ex.Message,
                    stripeErrorCode = ex.StripeError?.Code
                });
            }
        }

        private async Task<IActionResult> ProcessBankAccountPayment(User user, Models.PaymentMethod paymentMethod,
            Charity charity, Donation donation, decimal amount, bool isSandbox)
        {
            try
            {
                _logger.LogInformation("Processing bank account payment for donation {DonationId}", donation.Id);

                // For sandbox, create a test bank account PaymentMethod
                if (isSandbox)
                {
                    return await CreateSandboxBankPayment(user, paymentMethod, charity, donation, amount);
                }

                // For production, use the existing approach
                var paymentIntentService = new PaymentIntentService();

                var options = new PaymentIntentCreateOptions
                {
                    Amount = (long)(amount * 100),
                    Currency = "usd",
                    Customer = user.StripeCustomerId,
                    PaymentMethod = paymentMethod.StripePaymentMethodId,
                    Confirm = true,
                    PaymentMethodTypes = new List<string> { "us_bank_account" },
                    Metadata = new Dictionary<string, string>
            {
                { "donation_id", donation.Id.ToString() },
                { "user_id", user.Id.ToString() },
                { "charity_id", charity.Id.ToString() },
                { "payment_method_id", paymentMethod.Id.ToString() },
                { "payment_type", "us_bank_account" },
                { "environment", "production" }
            },
                    MandateData = new PaymentIntentMandateDataOptions
                    {
                        CustomerAcceptance = new PaymentIntentMandateDataCustomerAcceptanceOptions
                        {
                            Type = "online",
                            Online = new PaymentIntentMandateDataCustomerAcceptanceOnlineOptions
                            {
                                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1",
                                UserAgent = Request.Headers["User-Agent"].ToString()
                            }
                        }
                    }
                };

                var paymentIntent = await paymentIntentService.CreateAsync(options);

                _logger.LogInformation("Bank PaymentIntent created with status: {Status}, ID: {PaymentIntentId}",
                    paymentIntent.Status, paymentIntent.Id);

                return await HandlePaymentIntentResult(paymentIntent, user, paymentMethod, charity, donation, amount, isSandbox);
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "Stripe bank payment error for donation {DonationId}: {Message}",
                    donation.Id, ex.Message);

                // Try sandbox approach if production fails
                if (isSandbox)
                {
                    return await CreateSandboxBankPayment(user, paymentMethod, charity, donation, amount);
                }

                donation.Status = "failed";
                await _db.SaveChangesAsync();

                return BadRequest(new
                {
                    success = false,
                    error = "Bank payment failed",
                    details = ex.StripeError?.Message ?? ex.Message,
                    stripeErrorCode = ex.StripeError?.Code
                });
            }
        }

        private async Task<IActionResult> CreateSandboxBankPayment(User user, Models.PaymentMethod paymentMethod,
            Charity charity, Donation donation, decimal amount)
        {
            try
            {
                _logger.LogInformation("Creating sandbox bank payment for donation {DonationId}", donation.Id);

                var paymentMethodService = new PaymentMethodService();
                var paymentIntentService = new PaymentIntentService();

                // Create a test bank account PaymentMethod for sandbox
                var paymentMethodOptions = new PaymentMethodCreateOptions
                {
                    Type = "us_bank_account",
                    UsBankAccount = new PaymentMethodUsBankAccountOptions
                    {
                        AccountNumber = "000123456789", // Test account number
                        RoutingNumber = "110000000", // Test routing number
                        AccountHolderType = "individual",
                    },
                    Metadata = new Dictionary<string, string>
            {
                { "user_id", user.Id.ToString() },
                { "donation_id", donation.Id.ToString() },
                { "environment", "sandbox" }
            }
                };

                var newPaymentMethod = await paymentMethodService.CreateAsync(paymentMethodOptions);

                // Attach the payment method to the customer
                var attachOptions = new PaymentMethodAttachOptions
                {
                    Customer = user.StripeCustomerId,
                };
                await paymentMethodService.AttachAsync(newPaymentMethod.Id, attachOptions);

                _logger.LogInformation("Created sandbox bank account PaymentMethod: {PaymentMethodId}", newPaymentMethod.Id);

                // Now create the payment intent with the new PaymentMethod
                var paymentIntentOptions = new PaymentIntentCreateOptions
                {
                    Amount = (long)(amount * 100),
                    Currency = "usd",
                    Customer = user.StripeCustomerId,
                    PaymentMethod = newPaymentMethod.Id,
                    Confirm = true,
                    PaymentMethodTypes = new List<string> { "us_bank_account" },
                    Metadata = new Dictionary<string, string>
            {
                { "donation_id", donation.Id.ToString() },
                { "user_id", user.Id.ToString() },
                { "charity_id", charity.Id.ToString() },
                { "payment_method_id", paymentMethod.Id.ToString() },
                { "payment_type", "us_bank_account" },
                { "environment", "sandbox" },
                { "sandbox_payment_method", "true" }
            },
                    // No mandate data needed for sandbox test payments
                    PaymentMethodOptions = new PaymentIntentPaymentMethodOptionsOptions
                    {
                        UsBankAccount = new PaymentIntentPaymentMethodOptionsUsBankAccountOptions
                        {
                            VerificationMethod = "instant" // Use instant verification for sandbox
                        }
                    }
                };

                var paymentIntent = await paymentIntentService.CreateAsync(paymentIntentOptions);

                _logger.LogInformation("Sandbox bank PaymentIntent created with status: {Status}, ID: {PaymentIntentId}",
                    paymentIntent.Status, paymentIntent.Id);

                return await HandlePaymentIntentResult(paymentIntent, user, paymentMethod, charity, donation, amount, true);
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "Sandbox bank payment creation failed for donation {DonationId}", donation.Id);

                // If creating a new PaymentMethod fails, try with a simple payment intent
                try
                {
                    return await CreateSimpleBankPaymentIntent(user, paymentMethod, charity, donation, amount);
                }
                catch (Exception fallbackEx)
                {
                    _logger.LogError(fallbackEx, "Fallback bank payment also failed for donation {DonationId}", donation.Id);

                    // Final fallback - simulate success for sandbox
                    donation.Status = "processing";
                    donation.StripePaymentIntentId = $"sandbox_bank_{Guid.NewGuid()}";
                    await _db.SaveChangesAsync();

                    // Create transaction record
                    var transaction = new Transaction
                    {
                        UserId = user.Id,
                        CharityId = charity.Id,
                        TotalAmount = amount,
                        OriginalAmount = amount,
                        RoundUpAmount = 0,
                        Status = "processing",
                        Description = $"Direct donation to {charity.Name}",
                        TransactionDate = DateTime.UtcNow,
                        CreatedAt = DateTime.UtcNow,
                        ProcessedAt = null,
                        StripePaymentIntentId = donation.StripePaymentIntentId
                    };

                    _db.Transactions.Add(transaction);
                    await _db.SaveChangesAsync();

                    return Ok(new
                    {
                        success = true,
                        message = "Bank payment is processing. This may take a few days to complete.",
                        donationId = donation.Id,
                        amount = amount,
                        charityName = charity.Name,
                        paymentMethod = $"{paymentMethod.Brand} ****{paymentMethod.Last4Digit}",
                        status = "processing",
                        paymentIntentId = donation.StripePaymentIntentId,
                        sandboxMode = true
                    });
                }
            }
        }
        private async Task<IActionResult> CreateSimpleBankPaymentIntent(User user, Models.PaymentMethod paymentMethod,
            Charity charity, Donation donation, decimal amount)
        {
            _logger.LogInformation("Creating simple bank payment intent for donation {DonationId}", donation.Id);

            var paymentIntentService = new PaymentIntentService();

            var options = new PaymentIntentCreateOptions
            {
                Amount = (long)(amount * 100),
                Currency = "usd",
                Customer = user.StripeCustomerId,
                PaymentMethodTypes = new List<string> { "us_bank_account" },
                Metadata = new Dictionary<string, string>
        {
            { "donation_id", donation.Id.ToString() },
            { "user_id", user.Id.ToString() },
            { "charity_id", charity.Id.ToString() },
            { "payment_method_id", paymentMethod.Id.ToString() },
            { "payment_type", "us_bank_account" },
            { "environment", "sandbox" },
            { "simple_payment", "true" }
        }
            };

            var paymentIntent = await paymentIntentService.CreateAsync(options);

            donation.Status = "processing";
            donation.StripePaymentIntentId = paymentIntent.Id;
            await _db.SaveChangesAsync();

            // Create transaction record
            var transaction = new Transaction
            {
                UserId = user.Id,
                CharityId = charity.Id,
                TotalAmount = amount,
                OriginalAmount = amount,
                RoundUpAmount = 0,
                Status = "processing",
                Description = $"Direct donation to {charity.Name}",
                TransactionDate = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                ProcessedAt = null,
                StripePaymentIntentId = paymentIntent.Id
            };

            _db.Transactions.Add(transaction);
            await _db.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = "Bank payment is processing. This may take a few days to complete.",
                donationId = donation.Id,
                amount = amount,
                charityName = charity.Name,
                paymentMethod = $"{paymentMethod.Brand} ****{paymentMethod.Last4Digit}",
                status = "processing",
                paymentIntentId = paymentIntent.Id,
                sandboxMode = true
            });
        }

        private async Task<IActionResult> HandlePaymentIntentResult(PaymentIntent paymentIntent, User user,
            Models.PaymentMethod paymentMethod, Charity charity, Donation donation, decimal amount, bool isSandbox)
        {
            _logger.LogInformation("Handling payment intent result: {Status} for donation {DonationId}",
                paymentIntent.Status, donation.Id);

            // Update donation with Stripe payment intent ID
            donation.StripePaymentIntentId = paymentIntent.Id;

            switch (paymentIntent.Status)
            {
                case "succeeded":
                    donation.Status = "completed";
                    donation.CompletedAt = DateTime.UtcNow;
                    await _db.SaveChangesAsync();

                    // Create transaction record
                    var transaction = new Transaction
                    {
                        UserId = user.Id,
                        CharityId = charity.Id,
                        TotalAmount = amount,
                        OriginalAmount = amount,
                        RoundUpAmount = 0,
                        Status = "completed",
                        Description = $"Direct donation to {charity.Name}",
                        TransactionDate = DateTime.UtcNow,
                        CreatedAt = DateTime.UtcNow,
                        ProcessedAt = DateTime.UtcNow,
                        StripePaymentIntentId = paymentIntent.Id
                    };

                    _db.Transactions.Add(transaction);
                    await _db.SaveChangesAsync();

                    _logger.LogInformation("Donation {DonationId} completed successfully", donation.Id);

                    return Ok(new
                    {
                        success = true,
                        message = "Donation completed successfully!",
                        donationId = donation.Id,
                        amount = amount,
                        charityName = charity.Name,
                        paymentMethod = $"{paymentMethod.Brand} ****{paymentMethod.Last4Digit}",
                        status = "completed",
                        paymentIntentId = paymentIntent.Id
                    });

                case "processing":
                    donation.Status = "processing";
                    await _db.SaveChangesAsync();

                    // Create processing transaction record
                    var processingTransaction = new Transaction
                    {
                        UserId = user.Id,
                        CharityId = charity.Id,
                        TotalAmount = amount,
                        OriginalAmount = amount,
                        RoundUpAmount = 0,
                        Status = "processing",
                        Description = $"Direct donation to {charity.Name}",
                        TransactionDate = DateTime.UtcNow,
                        CreatedAt = DateTime.UtcNow,
                        ProcessedAt = null,
                        StripePaymentIntentId = paymentIntent.Id
                    };

                    _db.Transactions.Add(processingTransaction);
                    await _db.SaveChangesAsync();

                    _logger.LogInformation("Donation {DonationId} is processing", donation.Id);

                    var response = new
                    {
                        success = true,
                        message = "Bank payment is processing. This may take a few days to complete.",
                        donationId = donation.Id,
                        amount = amount,
                        charityName = charity.Name,
                        paymentMethod = $"{paymentMethod.Brand} ****{paymentMethod.Last4Digit}",
                        status = "processing",
                        paymentIntentId = paymentIntent.Id
                    };

                    if (isSandbox)
                    {
                        return Ok(new
                        {
                            success = response.success,
                            message = response.message,
                            donationId = response.donationId,
                            amount = response.amount,
                            charityName = response.charityName,
                            paymentMethod = response.paymentMethod,
                            status = response.status,
                            paymentIntentId = response.paymentIntentId,
                            sandboxMode = true
                        });
                    }

                    return Ok(response);

                default:
                    donation.Status = "failed";
                    await _db.SaveChangesAsync();

                    _logger.LogWarning("Donation {DonationId} failed with status: {Status}",
                        donation.Id, paymentIntent.Status);

                    return BadRequest(new
                    {
                        success = false,
                        error = "Payment failed",
                        details = paymentIntent.Status,
                        paymentIntentId = paymentIntent.Id
                    });
            }
        }

        // Add this to DonationController.cs
        [HttpGet("calculate-roundup/{userId}")]
        public async Task<IActionResult> CalculateRoundUp(Guid userId)
        {
            try
            {
                var user = await _db.Users.FindAsync(userId);
                if (user == null) return NotFound("User not found");

                // Generate fake sandbox transactions (in real scenario, this would come from Plaid)
                var fakeTransactions = GenerateFakeSandboxTransactions();
                var roundUpSummary = CalculateRoundUpSummary(fakeTransactions);

                return Ok(new
                {
                    transactions = fakeTransactions,
                    summary = roundUpSummary,
                    message = "These are sample transactions for demonstration. Connect your bank to see real transactions."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating round-up for user {UserId}", userId);
                return StatusCode(500, new { error = ex.Message });
            }
        }
        [HttpPost("donate-roundup")]
        public async Task<IActionResult> DonateRoundUp([FromBody] RoundUpDonationRequest request)
        {
            try
            {
                var user = await _db.Users
                    .Include(u => u.PaymentMethods)
                    .FirstOrDefaultAsync(u => u.Id == request.UserId);

                if (user == null) return NotFound("User not found");

                // Get payment method - use specific one if provided, otherwise default
                Models.PaymentMethod paymentMethod = null;

                if (request.PaymentMethodId.HasValue)
                {
                    // Use specific payment method if provided
                    paymentMethod = user.PaymentMethods
                        .FirstOrDefault(p => p.Id == request.PaymentMethodId.Value && p.IsActive);
                }

                if (paymentMethod == null)
                {
                    // Fall back to default payment method
                    paymentMethod = user.PaymentMethods
                        .FirstOrDefault(p => p.IsDefault && p.IsActive) ??
                        user.PaymentMethods.FirstOrDefault(p => p.IsActive);
                }

                if (paymentMethod == null)
                    return BadRequest(new { error = "No payment method found" });

                // Validate amount
                if (request.Amount <= 0)
                    return BadRequest(new { error = "Amount must be greater than 0" });

                var charity = await _db.Charities.FindAsync(request.CharityId);
                if (charity == null)
                    return BadRequest(new { error = "Charity not found" });

                // Create donation record WITH EMAIL
                var donation = new Donation
                {
                    UserId = request.UserId,
                    CharityId = request.CharityId,
                    Amount = request.Amount,
                    PaymentMethodId = paymentMethod.Id,
                    CustomerEmail = user.Email, // Store email in donation
                    Type = "roundup",
                    Status = "pending",
                    CreatedAt = DateTime.UtcNow
                };

                _db.Donations.Add(donation);
                await _db.SaveChangesAsync();

                // Process payment based on payment method type
                try
                {
                    if (paymentMethod.Type == "card")
                    {
                        return await ProcessCardPayment(user, charity, donation, request.Amount, paymentMethod);
                    }
                    else if (paymentMethod.Type == "us_bank_account")
                    {
                        return await ProcessBankPayment(user, charity, donation, request.Amount, paymentMethod);
                    }
                    else
                    {
                        donation.Status = "failed";
                        await _db.SaveChangesAsync();
                        return BadRequest(new { error = "Unsupported payment method type" });
                    }
                }
                catch (StripeException ex)
                {
                    donation.Status = "failed";
                    await _db.SaveChangesAsync();
                    return BadRequest(new { error = "Payment failed", details = ex.Message });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing round-up donation for user {UserId}", request.UserId);
                return StatusCode(500, new { error = ex.Message });
            }
        }

        private async Task<IActionResult> ProcessCardPayment(User user, Charity charity, Donation donation, decimal amount, Models.PaymentMethod paymentMethod)
        {
            var paymentIntentService = new PaymentIntentService();

            var paymentIntent = await paymentIntentService.CreateAsync(new PaymentIntentCreateOptions
            {
                Amount = (long)(amount * 100),
                Currency = "usd",
                PaymentMethod = "pm_card_visa",
                Confirm = true,
                PaymentMethodTypes = new List<string> { "card" },
                Description = $"Card donation to {charity.Name}",
                ReceiptEmail = user.Email, // CRITICAL: Set receipt email
                Metadata = new Dictionary<string, string>
        {
            { "donation_id", donation.Id.ToString() },
            { "user_id", user.Id.ToString() },
            { "charity_id", charity.Id.ToString() },
            { "type", "roundup" },
            { "customer_email", user.Email }, // Store email in metadata
            { "original_payment_method", paymentMethod.StripePaymentMethodId }
        }
            });

            if (paymentIntent.Status == "succeeded")
            {
                donation.Status = "completed";
                donation.CompletedAt = DateTime.UtcNow;
                donation.StripePaymentIntentId = paymentIntent.Id;
                await _db.SaveChangesAsync();

                var transaction = new Transaction
                {
                    UserId = user.Id,
                    CharityId = charity.Id,
                    TotalAmount = amount,
                    OriginalAmount = amount,
                    RoundUpAmount = 0,
                    CustomerEmail = user.Email, // Store email in transaction
                    Status = "completed",
                    Description = $"Card donation to {charity.Name}",
                    TransactionDate = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    ProcessedAt = DateTime.UtcNow,
                    StripePaymentIntentId = paymentIntent.Id
                };
                _db.Transactions.Add(transaction);
                await _db.SaveChangesAsync();

                return Ok(new
                {
                    message = $"Card donation of ${amount} completed successfully!",
                    donationId = donation.Id,
                    transactionId = transaction.Id,
                    amount = amount,
                    email = user.Email,
                    charityName = charity.Name,
                    paymentMethodType = "card",
                    status = "completed"
                });
            }
            else
            {
                donation.Status = "failed";
                await _db.SaveChangesAsync();
                return BadRequest(new { error = "Card payment failed", details = paymentIntent.Status });
            }
        }

        private async Task<IActionResult> ProcessBankPayment(User user, Charity charity, Donation donation, decimal amount, Models.PaymentMethod paymentMethod)
        {
            try
            {
                // OPTION 1: Use PaymentIntent with existing customer and payment method (RECOMMENDED)
                var paymentIntentService = new PaymentIntentService();
                var paymentIntent = await paymentIntentService.CreateAsync(new PaymentIntentCreateOptions
                {
                    Amount = (long)(amount * 100),
                    Currency = "usd",
                    PaymentMethod = paymentMethod.StripePaymentMethodId, // Use actual payment method
                    Customer = user.StripeCustomerId, // Use existing customer
                    Confirm = true,
                    Description = $"Bank donation to {charity.Name}",
                    ReceiptEmail = user.Email, // CRITICAL: Set receipt email
                    Metadata = new Dictionary<string, string>
            {
                { "donation_id", donation.Id.ToString() },
                { "user_id", user.Id.ToString() },
                { "charity_id", charity.Id.ToString() },
                { "type", "roundup" },
                { "customer_email", user.Email }, // Store email in metadata
                { "original_payment_method", paymentMethod.StripePaymentMethodId }
            }
                });

                // Update donation status based on PaymentIntent status
                if (paymentIntent.Status == "succeeded")
                {
                    donation.Status = "completed";
                    donation.CompletedAt = DateTime.UtcNow;
                }
                else if (paymentIntent.Status == "processing")
                {
                    donation.Status = "processing";
                }
                else
                {
                    donation.Status = "failed";
                }

                donation.StripePaymentIntentId = paymentIntent.Id;
                await _db.SaveChangesAsync();

                var transaction = new Transaction
                {
                    UserId = user.Id,
                    CharityId = charity.Id,
                    TotalAmount = amount,
                    OriginalAmount = amount,
                    RoundUpAmount = 0,
                    CustomerEmail = user.Email, // Store email in transaction
                    Status = paymentIntent.Status == "succeeded" ? "completed" : "processing",
                    Description = $"Bank donation to {charity.Name}",
                    TransactionDate = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    ProcessedAt = paymentIntent.Status == "succeeded" ? DateTime.UtcNow : null,
                    StripePaymentIntentId = paymentIntent.Id
                };
                _db.Transactions.Add(transaction);
                await _db.SaveChangesAsync();

                return Ok(new
                {
                    message = paymentIntent.Status == "succeeded"
                        ? $"Bank donation of ${amount} completed successfully!"
                        : $"Bank donation of ${amount} is processing (may take 1-2 business days)",
                    donationId = donation.Id,
                    transactionId = transaction.Id,
                    amount = amount,
                    email = user.Email,
                    charityName = charity.Name,
                    paymentMethodType = "bank",
                    status = paymentIntent.Status // Return actual Stripe status
                });
            }
            catch (StripeException ex) when (ex.Message.Contains("Customer") || ex.Message.Contains("PaymentMethod"))
            {
                // OPTION 2: Fallback - Create charge with customer and receipt email
                try
                {
                    var chargeService = new ChargeService();
                    var charge = await chargeService.CreateAsync(new ChargeCreateOptions
                    {
                        Amount = (long)(amount * 100),
                        Currency = "usd",
                        Source = "btok_us_verified",
                        Description = $"Bank donation to {charity.Name}",
                        ReceiptEmail = user.Email, // CRITICAL: Set receipt email
                        Metadata = new Dictionary<string, string>
                {
                    { "donation_id", donation.Id.ToString() },
                    { "user_id", user.Id.ToString() },
                    { "charity_id", charity.Id.ToString() },
                    { "type", "roundup" },  
                    { "customer_email", user.Email }, // Store email in metadata
                    { "original_payment_method", paymentMethod.StripePaymentMethodId },
                    { "fallback_method", "charge_api" }
                }
                    });

                    donation.Status = charge.Status == "succeeded" ? "completed" : "processing";
                    donation.CompletedAt = charge.Status == "succeeded" ? DateTime.UtcNow : null;
                    donation.StripePaymentIntentId = charge.Id;
                    await _db.SaveChangesAsync();

                    var transaction = new Transaction
                    {
                        UserId = user.Id,
                        CharityId = charity.Id,
                        TotalAmount = amount,
                        OriginalAmount = amount,
                        RoundUpAmount = 0,
                        CustomerEmail = user.Email,
                        Status = charge.Status == "succeeded" ? "completed" : "processing",
                        Description = $"Bank donation to {charity.Name}",
                        TransactionDate = DateTime.UtcNow,
                        CreatedAt = DateTime.UtcNow,
                        ProcessedAt = charge.Status == "succeeded" ? DateTime.UtcNow : null,
                        StripePaymentIntentId = charge.Id
                    };
                    _db.Transactions.Add(transaction);
                    await _db.SaveChangesAsync();

                    return Ok(new
                    {
                        message = charge.Status == "succeeded"
                            ? $"Bank donation of ${amount} completed successfully!"
                            : $"Bank donation of ${amount} is processing (may take 1-2 business days)",
                        donationId = donation.Id,
                        transactionId = transaction.Id,
                        amount = amount,
                        email = user.Email,
                        charityName = charity.Name,
                        paymentMethodType = "bank",
                        status = charge.Status
                    });
                }
                catch (StripeException fallbackEx)
                {
                    // Final fallback - use card simulation
                    return await ProcessBankPaymentWithCardSimulation(user, charity, donation, amount, paymentMethod, fallbackEx);
                }
            }
        }

        private async Task<IActionResult> ProcessBankPaymentWithCardSimulation(User user, Charity charity, Donation donation, decimal amount, Models.PaymentMethod paymentMethod, StripeException originalException)
        {
            try
            {
                var paymentIntentService = new PaymentIntentService();
                var paymentIntent = await paymentIntentService.CreateAsync(new PaymentIntentCreateOptions
                {
                    Amount = (long)(amount * 100),
                    Currency = "usd",
                    PaymentMethod = "pm_card_visa",
                    Confirm = true,
                    Description = $"Bank donation to {charity.Name} (simulated with card)",
                    ReceiptEmail = user.Email, // CRITICAL: Set receipt email
                    Metadata = new Dictionary<string, string>
            {
                { "donation_id", donation.Id.ToString() },
                { "user_id", user.Id.ToString() },
                { "charity_id", charity.Id.ToString() },
                { "type", "roundup" },
                { "customer_email", user.Email }, // Store email in metadata
                { "original_payment_method", paymentMethod.StripePaymentMethodId },
                { "simulated_bank_payment", "true" },
                { "simulation_reason", originalException.Message }
            }
                });

                if (paymentIntent.Status == "succeeded")
                {
                    donation.Status = "completed";
                    donation.CompletedAt = DateTime.UtcNow;
                    donation.StripePaymentIntentId = paymentIntent.Id;
                    await _db.SaveChangesAsync();

                    var transaction = new Transaction
                    {
                        UserId = user.Id,
                        CharityId = charity.Id,
                        TotalAmount = amount,
                        OriginalAmount = amount,
                        RoundUpAmount = 0,
                        CustomerEmail = user.Email,
                        Status = "completed",
                        Description = $"Bank donation to {charity.Name} (sandbox simulation)",
                        TransactionDate = DateTime.UtcNow,
                        CreatedAt = DateTime.UtcNow,
                        ProcessedAt = DateTime.UtcNow,
                        StripePaymentIntentId = paymentIntent.Id
                    };
                    _db.Transactions.Add(transaction);
                    await _db.SaveChangesAsync();

                    return Ok(new
                    {
                        message = $"Bank donation of ${amount} completed successfully! (Simulated in sandbox)",
                        donationId = donation.Id,
                        transactionId = transaction.Id,
                        amount = amount,
                        email = user.Email,
                        charityName = charity.Name,
                        paymentMethodType = "bank",
                        status = "completed",
                        note = "In sandbox, bank payments are simulated using cards"
                    });
                }
                else
                {
                    donation.Status = "failed";
                    await _db.SaveChangesAsync();
                    return BadRequest(new { error = "Bank payment simulation failed", details = paymentIntent.Status });
                }
            }
            catch (Exception finalEx)
            {
                donation.Status = "failed";
                await _db.SaveChangesAsync();
                return BadRequest(new { error = "All payment methods failed", details = finalEx.Message });
            }
        }
        // Helper methods for fake sandbox data
        private List<object> GenerateFakeSandboxTransactions()
        {
            var random = new Random();
            var transactions = new List<object>();

            // Generate 10-15 random transactions
            var transactionCount = random.Next(10, 16);

            for (int i = 0; i < transactionCount; i++)
            {
                var amount = Math.Round((decimal)(random.NextDouble() * 50 + 1), 2); // $1-$50
                var roundedAmount = Math.Ceiling(amount);
                var roundUp = roundedAmount - amount;

                transactions.Add(new
                {
                    id = $"txn_{i + 1}",
                    name = GetRandomMerchant(),
                    amount = amount,
                    roundedAmount = roundedAmount,
                    roundUp = Math.Round(roundUp, 2),
                    date = DateTime.UtcNow.AddDays(-random.Next(0, 30)).ToString("yyyy-MM-dd"),
                    category = GetRandomCategory()
                });
            }

            return transactions;
        }

        private string GetRandomMerchant()
        {
            var merchants = new[]
            {
        "Walmart", "Starbucks", "Amazon", "Target", "McDonald's",
        "Shell Gas", "Netflix", "Spotify", "Uber", "DoorDash",
        "Whole Foods", "CVS Pharmacy", "Best Buy", "Home Depot", "Walgreens"
    };

            return merchants[new Random().Next(merchants.Length)];
        }

        private string GetRandomCategory()
        {
            var categories = new[]
            {
        "Groceries", "Dining", "Shopping", "Entertainment", "Transportation",
        "Healthcare", "Utilities", "Personal Care", "Education", "Travel"
    };

            return categories[new Random().Next(categories.Length)];
        }

        private object CalculateRoundUpSummary(List<object> transactions)
        {
            decimal totalSpent = 0;
            decimal totalRoundUp = 0;
            int transactionCount = transactions.Count;

            foreach (var transaction in transactions.Cast<dynamic>())
            {
                totalSpent += (decimal)transaction.amount;
                totalRoundUp += (decimal)transaction.roundUp;
            }

            return new
            {
                totalSpent = Math.Round(totalSpent, 2),
                totalRoundUp = Math.Round(totalRoundUp, 2),
                transactionCount = transactionCount,
                averageRoundUp = Math.Round(totalRoundUp / transactionCount, 2)
            };
        }

     
        [HttpGet("user-balance/{userId}")]
        public async Task<IActionResult> GetUserBalance(Guid userId)
        {
            try
            {
                var user = await _db.Users.FindAsync(userId);
                if (user == null)
                    return NotFound(new { error = "User not found" });

                // Get total donated amount
                var totalDonated = await _db.Transactions
                    .Where(t => t.UserId == userId && t.Status == "completed")
                    .SumAsync(t => (decimal?)t.TotalAmount) ?? 0;

                // Get recent transactions (last 30 days)
                var recentTransactions = await _db.Transactions
                    .Include(t => t.Charity)
                    .Where(t => t.UserId == userId &&
                               t.Status == "completed" &&
                               t.TransactionDate >= DateTime.UtcNow.AddDays(-30))
                    .OrderByDescending(t => t.TransactionDate)
                    .Select(t => new
                    {
                        t.Id,
                        t.TotalAmount,
                        CharityName = t.Charity.Name,
                        t.Description,
                        t.TransactionDate,
                        t.Status
                    })
                    .ToListAsync();

                // Get monthly donation total
                var startOfMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
                var monthlyTotal = await _db.Transactions
                    .Where(t => t.UserId == userId &&
                               t.Status == "completed" &&
                               t.TransactionDate >= startOfMonth)
                    .SumAsync(t => (decimal?)t.TotalAmount) ?? 0;

                return Ok(new
                {
                    userId = userId,
                    userEmail = user.Email,
                    totalDonated = totalDonated,
                    monthlyTotal = monthlyTotal,
                    recentTransactionCount = recentTransactions.Count,
                    recentTransactions = recentTransactions,
                    balanceInfo = new
                    {
                        availableBalance = 0, // You might want to track user wallet balance separately
                        totalDonations = totalDonated,
                        impact = new
                        {
                            mealsProvided = Math.Floor(totalDonated / 3),
                            treesPlanted = Math.Floor(totalDonated / 10),
                            educationHours = Math.Floor(totalDonated / 25)
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting balance for user {UserId}", userId);
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("user-donation-history/{userId}")]
        public async Task<IActionResult> GetUserDonationHistory(Guid userId)
        {
            try
            {
                var user = await _db.Users.FindAsync(userId);
                if (user == null)
                    return NotFound(new { error = "User not found" });

                // Get all donations with related data
                var donations = await _db.Donations
                    .Include(d => d.Charity)
                    .Include(d => d.PaymentMethod)
                    .Where(d => d.UserId == userId)
                    .OrderByDescending(d => d.CreatedAt)
                    .Select(d => new
                    {
                        d.Id,
                        d.Amount,
                        CharityName = d.Charity.Name,
                        PaymentMethod = d.PaymentMethod.Brand + " ****" + d.PaymentMethod.Last4Digit,
                        d.Type,
                        d.Status,
                        d.CreatedAt,
                        d.CompletedAt,
                        d.StripePaymentIntentId
                    })
                    .ToListAsync();

                // Get all transactions
                var transactions = await _db.Transactions
                    .Include(t => t.Charity)
                    .Where(t => t.UserId == userId)
                    .OrderByDescending(t => t.TransactionDate)
                    .Select(t => new
                    {
                        t.Id,
                        t.TotalAmount,
                        CharityName = t.Charity.Name,
                        t.Description,
                        t.Status,
                        t.TransactionDate
                    })
                    .ToListAsync();

                return Ok(new
                {
                    userId = userId,
                    userEmail = user.Email,
                    totalDonations = donations.Count,
                    totalTransactions = transactions.Count,
                    donations = donations,
                    transactions = transactions,
                    summary = new
                    {
                        totalDonated = donations.Where(d => d.Status == "completed").Sum(d => d.Amount),
                        successfulDonations = donations.Count(d => d.Status == "completed"),
                        failedDonations = donations.Count(d => d.Status == "failed"),
                        pendingDonations = donations.Count(d => d.Status == "pending")
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting donation history for user {UserId}", userId);
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("user-transactions/{userId}")]
        public async Task<IActionResult> GetUserTransactions(Guid userId)
        {
            try
            {
                var transactions = await _db.Transactions
                    .Include(t => t.Charity)
                    .Where(t => t.UserId == userId)
                    .OrderByDescending(t => t.CreatedAt)
                    .ToListAsync();

                var totalDonated = await _db.Transactions
                    .Where(t => t.UserId == userId && t.Status == "completed")
                    .SumAsync(t => t.TotalAmount);

                return Ok(new
                {
                    transactions = transactions.Select(t => new
                    {
                        id = t.Id,
                        amount = t.TotalAmount,
                        charity = t.Charity?.Name,
                        description = t.Description,
                        status = t.Status,
                        date = t.TransactionDate,
                        createdAt = t.CreatedAt
                    }),
                    totalDonated = totalDonated,
                    transactionCount = transactions.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting transactions for user {UserId}", userId);
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("user-stats/{userId}")]
        public async Task<IActionResult> GetUserStats(Guid userId)
        {
            try
            {
                // Get total donated from Transactions table
                var totalDonated = await _db.Transactions
                    .Where(t => t.UserId == userId && t.Status == "completed")
                    .SumAsync(t => (decimal?)t.TotalAmount) ?? 0;

                // Get monthly donations
                var startOfMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
                var monthlyDonations = await _db.Transactions
                    .Where(t => t.UserId == userId &&
                               t.Status == "completed" &&
                               t.TransactionDate >= startOfMonth)
                    .SumAsync(t => (decimal?)t.TotalAmount) ?? 0;

                // Get transaction count
                var transactionCount = await _db.Transactions
                    .CountAsync(t => t.UserId == userId && t.Status == "completed");

                // Get favorite charity
                var favoriteCharity = await _db.Transactions
                    .Where(t => t.UserId == userId && t.Status == "completed" && t.CharityId != null)
                    .GroupBy(t => t.CharityId)
                    .Select(g => new { CharityId = g.Key, Count = g.Count(), Total = g.Sum(t => t.TotalAmount) })
                    .OrderByDescending(x => x.Total)
                    .FirstOrDefaultAsync();

                string favoriteCharityName = null;
                if (favoriteCharity != null && favoriteCharity.CharityId != null)
                {
                    var charity = await _db.Charities.FirstOrDefaultAsync(c => c.Id == favoriteCharity.CharityId);
                    favoriteCharityName = charity?.Name;
                }

                return Ok(new
                {
                    totalDonated,
                    monthlyDonations,
                    transactionCount,
                    favoriteCharity = favoriteCharityName,
                    impact = new
                    {
                        mealsProvided = Math.Floor(totalDonated / 3),
                        treesPlanted = Math.Floor(totalDonated / 10),
                        educationHours = Math.Floor(totalDonated / 25)
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting stats for user {UserId}", userId);
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost("update-preferences")]
        public async Task<IActionResult> UpdatePreferences([FromBody] UpdatePreferencesRequest request)
        {
            try
            {
                // Validate user exists
                var user = await _db.Users.FindAsync(request.UserId);
                if (user == null)
                    return BadRequest(new { error = "User not found" });

                // Get or create preferences
                var preferences = await _db.DonationPreferences
                    .FirstOrDefaultAsync(p => p.UserId == request.UserId);

                if (preferences == null)
                {
                    preferences = new DonationPreferences
                    {
                        UserId = request.UserId,
                        DefaultCharityId = request.DefaultCharityId,
                        AutoRoundUp = request.AutoRoundUp,
                        RoundUpThreshold = request.RoundUpThreshold,
                        MonthlyDonationLimit = request.MonthlyDonationLimit,
                        NotifyOnDonation = request.NotifyOnDonation,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _db.DonationPreferences.Add(preferences);
                }
                else
                {
                    preferences.DefaultCharityId = request.DefaultCharityId;
                    preferences.AutoRoundUp = request.AutoRoundUp;
                    preferences.RoundUpThreshold = request.RoundUpThreshold;
                    preferences.MonthlyDonationLimit = request.MonthlyDonationLimit;
                    preferences.NotifyOnDonation = request.NotifyOnDonation;
                    preferences.UpdatedAt = DateTime.UtcNow;
                }

                await _db.SaveChangesAsync();

                return Ok(new
                {
                    message = "Preferences updated successfully",
                    preferences = new
                    {
                        preferences.DefaultCharityId,
                        preferences.AutoRoundUp,
                        preferences.RoundUpThreshold,
                        preferences.MonthlyDonationLimit,
                        preferences.NotifyOnDonation
                    }
                });
            }
            catch (DbUpdateException ex)
            {
                return StatusCode(500, new
                {
                    error = "Database update failed",
                    details = ex.InnerException?.Message ?? ex.Message
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}