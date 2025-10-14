namespace The_Charity.Models.DTOs
{
    public class CreateLinkTokenRequest
    {
        public string ClientUserId { get; set; }
        public string[] Products { get; set; } = new[] { "auth", "liabilities", "transactions" };
    }
}
