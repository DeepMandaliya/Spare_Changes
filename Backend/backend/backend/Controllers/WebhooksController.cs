using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stripe;
using The_Charity.AppDBContext;
using The_Charity.Models.DTOs;
using The_Charity.Services;
using The_Charity.Services.Service_Contracts;

namespace The_Charity.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class WebhooksController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly ILogger<WebhooksController> _logger;
        private readonly ITransactionService _transactionService;

        public WebhooksController(
            AppDbContext db,
            ILogger<WebhooksController> logger,
            ITransactionService transactionService)
        {
            _db = db;
            _logger = logger;
            _transactionService = transactionService;
        }

        // Stripe Webhook - Payment Status Updates
        [HttpPost("stripe")]
        public async Task<IActionResult> HandleStripeWebhook()
        {
            var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();

            try
            {
                var stripeEvent = EventUtility.ConstructEvent(
                    json,
                    Request.Headers["Stripe-Signature"],
                    "whsec_your_webhook_secret" // Should be in config
                );

                _logger.LogInformation("Stripe webhook received: {EventType}", stripeEvent.Type);

                switch (stripeEvent.Type)
                {
                    case "payment_intent.succeeded":
                        await HandlePaymentSucceeded(stripeEvent.Data.Object as PaymentIntent);
                        break;

                    case "payment_intent.payment_failed":
                        await HandlePaymentFailed(stripeEvent.Data.Object as PaymentIntent);
                        break;

                    case "payment_intent.processing":
                        await HandlePaymentProcessing(stripeEvent.Data.Object as PaymentIntent);
                        break;

                    default:
                        _logger.LogInformation("Unhandled Stripe event: {EventType}", stripeEvent.Type);
                        break;
                }

                return Ok();
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "Stripe webhook error");
                return BadRequest();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Webhook processing error");
                return StatusCode(500);
            }
        }

        // Plaid Webhook - Transaction Updates
        [HttpPost("plaid")]
        public async Task<IActionResult> HandlePlaidWebhook([FromBody] PlaidWebhookRequest request)
        {
            try
            {
                _logger.LogInformation("Plaid webhook received: {WebhookType} - {WebhookCode}",
                    request.WebhookType, request.WebhookCode);

                if (request.WebhookType == "TRANSACTIONS")
                {
                    await HandleTransactionsWebhook(request);
                }
                else if (request.WebhookType == "ITEM")
                {
                    await HandleItemWebhook(request);
                }

                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing Plaid webhook");
                return StatusCode(500);
            }
        }

        private async Task HandlePaymentSucceeded(PaymentIntent paymentIntent)
        {
            var transaction = await _db.Transactions
                .FirstOrDefaultAsync(t => t.StripePaymentIntentId == paymentIntent.Id);

            if (transaction != null)
            {
                transaction.Status = "completed";
                transaction.ProcessedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();

                _logger.LogInformation("Payment completed for transaction {TransactionId}", transaction.Id);

                // Here you could send email notification, update user stats, etc.
            }
        }

        private async Task HandlePaymentFailed(PaymentIntent paymentIntent)
        {
            var transaction = await _db.Transactions
                .FirstOrDefaultAsync(t => t.StripePaymentIntentId == paymentIntent.Id);

            if (transaction != null)
            {
                transaction.Status = "failed";
                await _db.SaveChangesAsync();

                _logger.LogWarning("Payment failed for transaction {TransactionId}", transaction.Id);

                // Here you could notify user, attempt retry, etc.
            }
        }

        private async Task HandlePaymentProcessing(PaymentIntent paymentIntent)
        {
            var transaction = await _db.Transactions
                .FirstOrDefaultAsync(t => t.StripePaymentIntentId == paymentIntent.Id);

            if (transaction != null)
            {
                transaction.Status = "processing";
                await _db.SaveChangesAsync();

                _logger.LogInformation("Payment processing for transaction {TransactionId}", transaction.Id);
            }
        }

        private async Task HandleTransactionsWebhook(PlaidWebhookRequest request)
        {
            if (request.WebhookCode == "DEFAULT_UPDATE" && !string.IsNullOrEmpty(request.ItemId))
            {
                // Find the user associated with this Plaid item
                var plaidItem = await _db.PlaidItems
                    .FirstOrDefaultAsync(p => p.ItemId == request.ItemId);

                if (plaidItem != null)
                {
                    // Process new transactions for round-up
                    await _transactionService.ProcessRoundUpForUser(plaidItem.UserId);
                    _logger.LogInformation("Processed transactions webhook for user {UserId}", plaidItem.UserId);
                }
            }
            else if (request.WebhookCode == "INITIAL_UPDATE")
            {
                _logger.LogInformation("Initial update received for item {ItemId}", request.ItemId);
            }
        }

        private async Task HandleItemWebhook(PlaidWebhookRequest request)
        {
            if (request.WebhookCode == "ERROR")
            {
                _logger.LogError("Plaid item error for item {ItemId}", request.ItemId);
                // Handle item errors (reconnect needed, etc.)
            }
        }
    }
}
