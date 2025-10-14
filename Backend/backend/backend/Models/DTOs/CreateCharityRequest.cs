namespace The_Charity.Models.DTOs
{
    public class CreateCharityRequest
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string LogoUrl { get; set; }
        public string Website { get; set; }
        public string StripeAccountId { get; set; }
    }
}
