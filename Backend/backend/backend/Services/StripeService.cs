using Stripe;

namespace The_Charity.Services
{
    public class StripeService
    {
        public StripeService(IConfiguration cfg)
        {
            StripeConfiguration.ApiKey = cfg["Stripe:SecretKey"];
        }

        public async Task<Customer> CreateCustomerAsync(string email, string name)
        {
            var options = new CustomerCreateOptions
            {
                Email = email,
                Name = name,
                Metadata = new Dictionary<string, string> { { "app", "roundup-charity" } }
            };
            var service = new CustomerService();
            return await service.CreateAsync(options);
        }

        public async Task<PaymentMethod> AttachPaymentMethodAsync(string paymentMethodId, string customerId)
        {
            var service = new PaymentMethodService();
            var options = new PaymentMethodAttachOptions { Customer = customerId };
            return await service.AttachAsync(paymentMethodId, options);
        }
        public async Task<PaymentIntent> CreatePaymentIntentAsync(long amount, string customerId, string paymentMethodId, string currency = "usd")
        {
            var options = new PaymentIntentCreateOptions
            {
                Amount = amount,
                Currency = currency,
                Customer = customerId,
                PaymentMethod = paymentMethodId,
                Confirm = true,
                OffSession = true,
                Metadata = new Dictionary<string, string>
                {
                    { "app", "roundup-charity" },
                    { "type", "roundup-donation" }
                }
            };

            var service = new PaymentIntentService();
            return await service.CreateAsync(options);
        }
        public async Task<PaymentIntent> CreateAchPaymentIntentAsync(long amount, string customerId, string paymentMethodId)
        {
            var options = new PaymentIntentCreateOptions
            {
                Amount = amount,
                Currency = "usd",
                Customer = customerId,
                PaymentMethod = paymentMethodId,
                PaymentMethodTypes = new List<string> { "us_bank_account" },
                Confirm = true,
                OffSession = true,
                MandateData = new PaymentIntentMandateDataOptions
                {
                    CustomerAcceptance = new PaymentIntentMandateDataCustomerAcceptanceOptions
                    {
                        Type = "online",
                        Online = new PaymentIntentMandateDataCustomerAcceptanceOnlineOptions
                        {
                            IpAddress = "0.0.0.0", // Will be set in controller
                            UserAgent = "RoundUp Charity App"
                        }
                    }
                }
            };

            var service = new PaymentIntentService();
            return await service.CreateAsync(options);
        }
        public async Task<List<PaymentMethod>> GetCustomerPaymentMethodsAsync(string customerId, string type = null)
        {
            var options = new PaymentMethodListOptions
            {
                Customer = customerId,
                Type = type
            };

            var service = new PaymentMethodService();
            var paymentMethods = await service.ListAsync(options);
            return paymentMethods.ToList();
        }
   
    }
}
