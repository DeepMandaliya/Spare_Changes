namespace The_Charity.Models.DTOs
{
    public class SwitchDefaultRequest
    {
        public Guid CurrentPaymentMethodId { get; set; }
        public Guid NewPaymentMethodId { get; set; }
    }
}
