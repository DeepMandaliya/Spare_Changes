namespace The_Charity.Models.DTOs
{
    public class ExchangePublicTokenRequest
    {
        public string PublicToken { get; set; }
        public Guid UserId { get; set; }
    }
}
