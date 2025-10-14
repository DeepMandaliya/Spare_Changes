using System.ComponentModel.DataAnnotations;

namespace The_Charity.Models
{
    public class PlaidItem
    {
        [Key]
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string ItemId { get; set; }
        public string AccessToken { get; set; }
        public string InstitutionName { get; set; }
        public string InstitutionId { get; set; }
        public string AccountsJson { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastSynced { get; set; }

        // Navigation properties
        public virtual User User { get; set; }
    }
}
