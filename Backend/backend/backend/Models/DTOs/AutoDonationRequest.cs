namespace The_Charity.Models.DTOs
{
    public class AutoDonationRequest
    {
        public Guid UserId { get; set; }
        public bool EnableAutoDonation { get; set; }
        public decimal RoundUpThreshold { get; set; } = 0.10m;
        public decimal MonthlyLimit { get; set; } = 50.00m;
        public int DonationDay { get; set; } = 1; // 1st of the month
    }
}
