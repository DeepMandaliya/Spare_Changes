namespace The_Charity.Models.DTOs
{
    public class PlaidWebhookRequest
    {
        public string WebhookType { get; set; }
        public string WebhookCode { get; set; }
        public string ItemId { get; set; }
        public List<string> AccountIds { get; set; }
    }
}
