namespace The_Charity.Models.DTOs
{
    public class RoundUpDonationRequest
    {
        public Guid UserId { get; set; }
        public Guid CharityId { get; set; }
        public decimal Amount { get; set; }
        public Guid? PaymentMethodId { get; set; }
    }
}
