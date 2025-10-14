namespace The_Charity.Models.DTOs
{
    public class UpdatePreferencesRequest
    {
        public Guid UserId { get; set; }
        public Guid DefaultCharityId { get; set; }
        public bool AutoRoundUp { get; set; } = true;
        public decimal RoundUpThreshold { get; set; } = 0.10m;
        public decimal MonthlyDonationLimit { get; set; } = 50.00m;
        public bool NotifyOnDonation { get; set; } = true;
    }
}
