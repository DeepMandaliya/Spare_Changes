using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stripe;
using System.Text.Json;
using The_Charity.AppDBContext;
using The_Charity.Models;
using The_Charity.Models.DTOs;
using The_Charity.Services;

namespace The_Charity.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PlaidController : ControllerBase
    {
        private readonly PlaidServices _plaid;
        private readonly AppDbContext _db;
        private readonly StripeService _stripeService;
        private readonly IConfiguration _configuration;
        private readonly ILogger _logger;

        public PlaidController(PlaidServices plaid, AppDbContext db, StripeService stripeService, IConfiguration configuration)
        {
            _plaid = plaid;
            _db = db;
            _stripeService = stripeService;
            _configuration = configuration;
            
        }

        [HttpPost("create-link-token")]
        public async Task<IActionResult> CreateLinkToken([FromBody] CreateLinkTokenRequest req)
        {
            var res = await _plaid.CreateLinkTokenAsync(req.ClientUserId, req.Products);
            var json = await res.Content.ReadAsStringAsync();
            return Content(json, "application/json");
        }

        // Controllers/PlaidController.cs
        [HttpPost("exchange-public-token")]
        public async Task<IActionResult> ExchangePublicToken([FromBody] ExchangePublicTokenRequest req)
        {
            var res = await _plaid.ExchangePublicTokenAsync(req.PublicToken);
            var json = await res.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("error_code", out var errorCode))
            {
                return BadRequest(new
                {
                    error = true,
                    code = errorCode.GetString(),
                    message = root.GetProperty("error_message").GetString()
                });
            }

            var accessToken = root.GetProperty("access_token").GetString();
            var itemId = root.GetProperty("item_id").GetString();

            // Get item info for institution name
            var itemRes = await _plaid.GetItemAsync(accessToken);
            var itemJson = await itemRes.Content.ReadAsStringAsync();
            var itemDoc = JsonDocument.Parse(itemJson);
            var institutionId = itemDoc.RootElement.GetProperty("item")
                .GetProperty("institution_id").GetString();

            var institutionName = await GetInstitutionName(institutionId);

            // Get accounts to populate AccountsJson
            var accountsRes = await _plaid.GetAccountsAsync(accessToken);
            var accountsJson = await accountsRes.Content.ReadAsStringAsync();

            // Store Plaid item with AccountsJson
            var plaidItem = new Models.PlaidItem
            {
                UserId = req.UserId,
                ItemId = itemId,
                AccessToken = accessToken,
                InstitutionName = institutionName,
                InstitutionId = institutionId,
                AccountsJson = accountsJson, // Set this to avoid NULL
                CreatedAt = DateTime.UtcNow
            };

            _db.PlaidItems.Add(plaidItem);
            await _db.SaveChangesAsync();

            return Ok(new
            {
                accessToken,
                itemId,
                institutionName,
                message = "Bank connected successfully"
            });
        }

        [HttpPost("sync-user-info/{plaidItemId}")]
        public async Task<IActionResult> SyncUserInfo(string plaidItemId)
        {
            try
            {
                var item = await _db.PlaidItems
                    .Include(p => p.User)
                    .FirstOrDefaultAsync(x => x.ItemId == plaidItemId);

                if (item == null) return NotFound("Plaid item not found");

                // Get item information to get institution details
                var itemRes = await _plaid.GetItemAsync(item.AccessToken);
                var itemJson = await itemRes.Content.ReadAsStringAsync();

                if (!itemRes.IsSuccessStatusCode)
                {
                    return BadRequest(new { error = "Failed to get item info", details = itemJson });
                }

                var itemDoc = JsonDocument.Parse(itemJson);
                var itemRoot = itemDoc.RootElement;

                // Extract institution information
                string institutionName = item.InstitutionName;
                if (itemRoot.TryGetProperty("item", out var itemElement) &&
                    itemElement.TryGetProperty("institution_id", out var institutionIdElement))
                {
                    var institutionId = institutionIdElement.GetString();
                    institutionName = await GetInstitutionName(institutionId);

                    // Update institution name if different
                    if (item.InstitutionName != institutionName)
                    {
                        item.InstitutionName = institutionName;
                        await _db.SaveChangesAsync();
                    }
                }

                return Ok(new
                {
                    message = "User info synced successfully",
                    institutionName = institutionName,
                    plaidItemId = plaidItemId
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    error = "Internal server error",
                    message = ex.Message
                });
            }
        }
        [HttpPost("get-payment-methods/{plaidItemId}")]
        public async Task<IActionResult> GetPaymentMethods(string plaidItemId)
        {
            var item = await _db.PlaidItems
                .FirstOrDefaultAsync(x => x.ItemId == plaidItemId);

            if (item == null) return NotFound();

            // Get bank accounts
            var accountsRes = await _plaid.GetAccountsAsync(item.AccessToken);
            var accountsJson = await accountsRes.Content.ReadAsStringAsync();
            var accountsDoc = JsonDocument.Parse(accountsJson);
            var accounts = accountsDoc.RootElement.GetProperty("accounts");

            // Get credit cards
            var creditCardsRes = await _plaid.GetCreditCardsAsync(item.AccessToken);
            var creditCardsJson = await creditCardsRes.Content.ReadAsStringAsync();
            var creditCardsDoc = JsonDocument.Parse(creditCardsJson);

            List<object> bankAccountsList = new List<object>();
            List<object> creditCardsList = new List<object>();

            // Process bank accounts
            foreach (var account in accounts.EnumerateArray())
            {
                var accountId = account.GetProperty("account_id").GetString();
                var subtype = account.GetProperty("subtype").GetString();

                if (subtype == "checking" || subtype == "savings")
                {
                    bankAccountsList.Add(new
                    {
                        account_id = accountId,
                        name = account.GetProperty("name").GetString(),
                        mask = account.GetProperty("mask").GetString(),
                        subtype = subtype,
                        type = account.GetProperty("type").GetString(),
                        balances = account.TryGetProperty("balances", out var balances) ?
                                  new { current = balances.TryGetProperty("current", out var current) ? current.GetDecimal() : 0 } :
                                  new { current = 0m }
                    });
                }
            }

            // Process credit cards
            if (creditCardsDoc.RootElement.TryGetProperty("liabilities", out var liabilities) &&
                liabilities.TryGetProperty("credit", out var credit))
            {
                foreach (var card in credit.EnumerateArray())
                {
                    var accountId = card.GetProperty("account_id").GetString();

                    creditCardsList.Add(new
                    {
                        account_id = accountId,
                        name = card.TryGetProperty("name", out var name) ? name.GetString() : "Credit Card",
                        mask = card.TryGetProperty("mask", out var mask) ? mask.GetString() : "0000",
                        subtype = "credit card",
                        type = "credit",
                        balances = card.TryGetProperty("balances", out var balances) ?
                                  new { current = balances.TryGetProperty("current", out var current) ? current.GetDecimal() : 0 } :
                                  new { current = 0m }
                    });
                }
            }

            return Ok(new
            {
                bankAccounts = bankAccountsList,
                creditCards = creditCardsList
            });
        }


        [HttpPost("create-stripe-bank-token")]
        public async Task<IActionResult> CreateStripeBankToken([FromBody] CreateStripeTokenRequest req)
        {
            try
            {
                var item = await _db.PlaidItems.FirstOrDefaultAsync(x => x.ItemId == req.PlaidItemId);
                if (item == null)
                    return NotFound("Plaid item not found");

                var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == item.UserId);
                if (user == null)
                    return NotFound("User not found");

                // 🧩 Always sandbox mode (no production logic at all)
                bool isSandbox = true;

                // 🧠 Generate fake verified PaymentMethod ID (no Stripe API call)
                string stripePaymentMethodId = $"pm_sandbox_ba_verified_{Guid.NewGuid():N}";

                // ✅ Save fake payment method directly to DB
                var hasExistingDefault = await _db.PaymentMethods
                    .AnyAsync(p => p.UserId == user.Id && p.IsDefault);

                var newPaymentMethod = new Models.PaymentMethod
                {
                    UserId = user.Id,
                    Type = "us_bank_account",
                    StripePaymentMethodId = stripePaymentMethodId,
                    Last4Digit = req.LastFour ?? "1111",
                    Brand = "Bank Account (Sandbox)",
                    IsDefault = !hasExistingDefault,
                    IsActive = true,
                    RequiresVerification = false,
                    CreatedAt = DateTime.UtcNow
                };

                _db.PaymentMethods.Add(newPaymentMethod);
                await _db.SaveChangesAsync();

                return Ok(new
                {
                    message = "✅ Sandbox bank account added successfully (no verification, no Stripe call).",
                    stripePaymentMethodId,
                    paymentMethodId = newPaymentMethod.Id,
                    isSandbox
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    error = "Internal error",
                    details = ex.Message
                });
            }
        }


        [HttpPost("create-stripe-card-token")]
        public async Task<IActionResult> CreateStripeCardToken([FromBody] CreateStripeTokenRequest req)
        {
            try
            {
                var item = await _db.PlaidItems.FirstOrDefaultAsync(x => x.ItemId == req.PlaidItemId);
                if (item == null) return NotFound("Plaid item not found");

                var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == item.UserId);
                if (user == null) return NotFound("User not found");

                var env = _configuration["Plaid:Environment"] ?? "sandbox";
                var isSandbox = env == "sandbox";

                string stripeToken;
                if (isSandbox)
                {
                    // Use valid Stripe test token for credit cards in sandbox
                    stripeToken = "pm_card_visa"; // Valid Stripe test token
                }
                else
                {
                    var res = await _plaid.CreateStripeCardTokenAsync(item.AccessToken, req.AccountId);
                    var json = await res.Content.ReadAsStringAsync();

                    if (!res.IsSuccessStatusCode)
                    {
                        return BadRequest(new { error = "Plaid API error", details = json });
                    }

                    var jsonDoc = JsonDocument.Parse(json);
                    var root = jsonDoc.RootElement;

                    if (root.TryGetProperty("error_code", out var errorCode))
                    {
                        return BadRequest(new
                        {
                            error = true,
                            code = errorCode.GetString(),
                            message = root.GetProperty("error_message").GetString()
                        });
                    }

                    if (!root.TryGetProperty("stripe_credit_card_token", out var tokenElement))
                    {
                        return BadRequest(new { error = "No stripe token found" });
                    }

                    stripeToken = tokenElement.GetString();
                }

                // Attach to Stripe customer
                var paymentMethod = await _stripeService.AttachPaymentMethodAsync(stripeToken, user.StripeCustomerId);

                // Save to database
                var paymentMethodRecord = new Models.PaymentMethod
                {
                    UserId = user.Id,
                    Type = "card",
                    StripePaymentMethodId = paymentMethod.Id,
                    Last4Digit = req.LastFour,
                    Brand = "Credit Card",
                    IsDefault = !await _db.PaymentMethods.AnyAsync(p => p.UserId == user.Id && p.IsDefault)
                };

                _db.PaymentMethods.Add(paymentMethodRecord);
                await _db.SaveChangesAsync();

                return Ok(new
                {
                    paymentMethodId = paymentMethod.Id,
                    type = "card",
                    lastFour = req.LastFour,
                    brand = "Credit Card",
                    isSandboxMock = isSandbox
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
        private async Task<string> GetInstitutionName(string institutionId)
        {
            try
            {
                var payload = new
                {
                    client_id = _plaid.GetType().GetField("_clientId", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(_plaid),
                    secret = _plaid.GetType().GetField("_secret", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(_plaid),
                    institution_id = institutionId,
                    country_codes = new[] { "US" }
                };

                var httpClient = _plaid.GetType().GetField("_http", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(_plaid) as HttpClient;
                var baseUrl = _plaid.GetType().GetField("_baseUrl", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(_plaid) as string;

                var response = await httpClient.PostAsJsonAsync($"{baseUrl}/institutions/get_by_id", payload);
                var json = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("institution", out var institution) &&
                        institution.TryGetProperty("name", out var name))
                    {
                        return name.GetString();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting institution name: {ex.Message}");
            }

            return "Unknown Institution";
        }
        [HttpGet("user-payment-methods/{userId}")]
        public async Task<IActionResult> GetUserPaymentMethods(Guid userId)
        {
            try
            {
                var paymentMethods = await _db.PaymentMethods
                    .Where(p => p.UserId == userId)
                    .OrderByDescending(p => p.IsDefault)
                    .ThenByDescending(p => p.IsActive)
                    .ThenBy(p => p.Id)
                    .Select(p => new
                    {
                        id = p.Id,
                        type = p.Type,
                        stripePaymentMethodId = p.StripePaymentMethodId,
                        lastFour = p.Last4Digit,
                        brand = p.Brand,
                        isDefault = p.IsDefault,
                        isActive = p.IsActive, // CRITICAL: Include this field
                        requiresVerification = p.RequiresVerification,
                        createdAt = p.CreatedAt,
                        canDelete = !p.IsDefault && p.IsActive
                    })
                    .ToListAsync();

                return Ok(paymentMethods);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost("users/{userId}/payment-methods/{paymentMethodId}/activate")]
        public async Task<IActionResult> ActivatePaymentMethod(Guid userId, Guid paymentMethodId)
        {
            try
            {
                var paymentMethod = await _db.PaymentMethods
                    .FirstOrDefaultAsync(p => p.Id == paymentMethodId && p.UserId == userId);

                if (paymentMethod == null)
                    return NotFound("Payment method not found");

                // For bank accounts, check if verification is required
                if (paymentMethod.Type == "us_bank_account" && paymentMethod.RequiresVerification)
                {
                    return BadRequest(new
                    {
                        error = "Bank account requires verification",
                        details = "Please complete bank account verification before activating",
                        paymentMethodId = paymentMethod.Id,
                        requiresVerification = true
                    });
                }

                paymentMethod.IsActive = true;
                await _db.SaveChangesAsync();

                return Ok(new
                {
                    message = "Payment method activated successfully",
                    paymentMethodId = paymentMethod.Id,
                    isActive = true,
                    type = paymentMethod.Type
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost("users/{userId}/payment-methods/{paymentMethodId}/set-default")]
        public async Task<IActionResult> SetDefaultPaymentMethod(Guid userId, Guid paymentMethodId)
        {
            try
            {
                var paymentMethod = await _db.PaymentMethods
                    .FirstOrDefaultAsync(p => p.Id == paymentMethodId && p.UserId == userId);

                if (paymentMethod == null)
                    return NotFound("Payment method not found");

                if (!paymentMethod.IsActive)
                {
                    return BadRequest(new
                    {
                        error = "Cannot set inactive payment method as default",
                        details = "Please activate the payment method first",
                        paymentMethodId = paymentMethod.Id,
                        isActive = false
                    });
                }

                // Get all user's payment methods
                var userPaymentMethods = await _db.PaymentMethods
                    .Where(p => p.UserId == userId)
                    .ToListAsync();

                // Remove default from all payment methods
                foreach (var pm in userPaymentMethods)
                {
                    pm.IsDefault = false;
                }

                // Set new default
                paymentMethod.IsDefault = true;

                // Update Stripe customer default
                try
                {
                    var user = await _db.Users.FindAsync(userId);
                    if (user != null)
                    {
                        var customerService = new CustomerService();
                        await customerService.UpdateAsync(user.StripeCustomerId, new CustomerUpdateOptions
                        {
                            InvoiceSettings = new CustomerInvoiceSettingsOptions
                            {
                                DefaultPaymentMethod = paymentMethod.StripePaymentMethodId
                            }
                        });
                    }
                }
                catch (Exception ex)
                {
                    //_logger.LogWarning("Failed to update Stripe customer default: {Message}", ex.Message);
                }

                await _db.SaveChangesAsync();

                return Ok(new
                {
                    message = "Default payment method updated successfully",
                    newDefault = new
                    {
                        id = paymentMethod.Id,
                        type = paymentMethod.Type,
                        lastFour = paymentMethod.Last4Digit,
                        brand = paymentMethod.Brand
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost("users/{userId}/payment-methods/switch-default")]
        public async Task<IActionResult> SwitchDefaultPaymentMethod(Guid userId, [FromBody] SwitchDefaultRequest request)
        {
            try
            {
                var currentPaymentMethod = await _db.PaymentMethods
                    .FirstOrDefaultAsync(p => p.Id == request.CurrentPaymentMethodId && p.UserId == userId);

                var newPaymentMethod = await _db.PaymentMethods
                    .FirstOrDefaultAsync(p => p.Id == request.NewPaymentMethodId && p.UserId == userId);

                if (currentPaymentMethod == null || newPaymentMethod == null)
                    return NotFound("Payment method not found");

                if (!newPaymentMethod.IsActive)
                    return BadRequest("New payment method is not active. Please activate it first.");

                // Switch defaults
                currentPaymentMethod.IsDefault = false;
                newPaymentMethod.IsDefault = true;

                // Update Stripe customer
                try
                {
                    var user = await _db.Users.FindAsync(userId);
                    if (user != null)
                    {
                        var customerService = new CustomerService();
                        await customerService.UpdateAsync(user.StripeCustomerId, new CustomerUpdateOptions
                        {
                            InvoiceSettings = new CustomerInvoiceSettingsOptions
                            {
                                DefaultPaymentMethod = newPaymentMethod.StripePaymentMethodId
                            }
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Stripe customer update failed: {Message}", ex.Message);
                }

                await _db.SaveChangesAsync();

                return Ok(new
                {
                    message = "Default payment method switched successfully",
                    oldDefault = new { id = currentPaymentMethod.Id, lastFour = currentPaymentMethod.Last4Digit },
                    newDefault = new { id = newPaymentMethod.Id, lastFour = newPaymentMethod.Last4Digit }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost("users/{userId}/payment-methods/{paymentMethodId}/deactivate")]
        public async Task<IActionResult> DeactivatePaymentMethod(Guid userId, Guid paymentMethodId)
        {
            try
            {
                var paymentMethod = await _db.PaymentMethods
                    .FirstOrDefaultAsync(p => p.Id == paymentMethodId && p.UserId == userId);

                if (paymentMethod == null)
                    return NotFound("Payment method not found");

                // Get user's active payment methods (excluding this one)
                var userActivePaymentMethods = await _db.PaymentMethods
                    .Where(p => p.UserId == userId && p.IsActive && p.Id != paymentMethodId)
                    .ToListAsync();

                // If this is the last active payment method
                if (userActivePaymentMethods.Count == 0)
                {
                    return BadRequest(new
                    {
                        error = "Cannot deactivate payment method",
                        details = "This is your only active payment method. Please add another payment method first."
                    });
                }

                // If this is the default payment method, set a new default
                if (paymentMethod.IsDefault)
                {
                    var newDefault = userActivePaymentMethods.First();
                    newDefault.IsDefault = true;
                }


                paymentMethod.IsDefault = false;

                await _db.SaveChangesAsync();

                return Ok(new
                {
                    message = "Payment method deactivated successfully",
                    paymentMethodId = paymentMethodId,
                    IsDefault = false
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}
