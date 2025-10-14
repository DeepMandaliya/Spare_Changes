namespace The_Charity.Models.DTOs
{
    public class CreateStripeTokenRequest
    {
        public string PlaidItemId { get; set; }
        public string AccountId { get; set; }
        public string LastFour { get; set; }
        public string BankName { get; set; }
    }
}
