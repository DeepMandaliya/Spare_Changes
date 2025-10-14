namespace The_Charity.Services
{
    public class PlaidServices
    {
        private readonly HttpClient _http;
        private readonly string _clientId;
        private readonly string _secret;
        private readonly string _baseUrl;
        public PlaidServices(HttpClient http, IConfiguration cfg)
        {
            _http = http;
            _clientId = cfg["Plaid:ClientId"];
            _secret = cfg["Plaid:Secret"];
            var env = cfg["Plaid:Environment"] ?? "sandbox";
            _baseUrl = env switch
            {
                "sandbox" => "https://sandbox.plaid.com",
                "development" => "https://development.plaid.com",
                _ => "https://production.plaid.com"
            };
        }
        public async Task<HttpResponseMessage> CreateLinkTokenAsync(string clientUserId, string[] products = null)
        {
            var payload = new
            {
                client_id = _clientId,
                secret = _secret,
                client_name = "RoundUp Charity App",
                products = products ?? new[] { "auth", "liabilities", "transactions" },
                country_codes = new[] { "US" },
                language = "en",
                user = new { client_user_id = clientUserId },
                transactions = new { days_requested = 30 }
            };
            return await _http.PostAsJsonAsync($"{_baseUrl}/link/token/create", payload);
        }

        public async Task<HttpResponseMessage> ExchangePublicTokenAsync(string publicToken)
        {
            var payload = new
            {
                client_id = _clientId,
                secret = _secret,
                public_token = publicToken
            };
            return await _http.PostAsJsonAsync($"{_baseUrl}/item/public_token/exchange", payload);
        }

        public async Task<HttpResponseMessage> GetAccountsAsync(string accessToken)
        {
            var payload = new
            {
                client_id = _clientId,
                secret = _secret,
                access_token = accessToken
            };
            return await _http.PostAsJsonAsync($"{_baseUrl}/accounts/get", payload);
        }

        public async Task<HttpResponseMessage> GetCreditCardsAsync(string accessToken)
        {
            var payload = new
            {
                client_id = _clientId,
                secret = _secret,
                access_token = accessToken
            };
            return await _http.PostAsJsonAsync($"{_baseUrl}/liabilities/get", payload);
        }
        public async Task<HttpResponseMessage> GetTransactionsAsync(string accessToken, DateTime startDate, DateTime endDate)
        {
            var payload = new
            {
                client_id = _clientId,
                secret = _secret,
                access_token = accessToken,
                start_date = startDate.ToString("yyyy-MM-dd"),
                end_date = endDate.ToString("yyyy-MM-dd")
            };
            return await _http.PostAsJsonAsync($"{_baseUrl}/transactions/get", payload);
        }

        public async Task<HttpResponseMessage> CreateStripeBankAccountTokenAsync(string accessToken, string accountId)
        {
            var payload = new
            {
                client_id = _clientId,
                secret = _secret,
                access_token = accessToken,
                account_id = accountId
            };
            return await _http.PostAsJsonAsync($"{_baseUrl}/processor/stripe/bank_account_token/create", payload);
        }
        public async Task<HttpResponseMessage> CreateStripeCardTokenAsync(string accessToken, string accountId)
        {
            var payload = new
            {
                client_id = _clientId,
                secret = _secret,
                access_token = accessToken,
                account_id = accountId
            };
            return await _http.PostAsJsonAsync($"{_baseUrl}/processor/stripe/credit_card_token/create", payload);
        }
        public async Task<HttpResponseMessage> GetItemAsync(string accessToken)
        {
            var payload = new
            {
                client_id = _clientId,
                secret = _secret,
                access_token = accessToken
            };
            return await _http.PostAsJsonAsync($"{_baseUrl}/item/get", payload);
        }
        public async Task<HttpResponseMessage> GetIdentityAsync(string accessToken)
        {
            var payload = new
            {
                client_id = _clientId,
                secret = _secret,
                access_token = accessToken
            };
            return await _http.PostAsJsonAsync($"{_baseUrl}/identity/get", payload);
        }

        public bool IsSandboxEnvironment() => _baseUrl.Contains("sandbox");
    
}
}
