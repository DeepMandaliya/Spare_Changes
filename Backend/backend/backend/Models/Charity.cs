using System.ComponentModel.DataAnnotations;

namespace The_Charity.Models
{
    public class Charity

    {
        [Key]
        public Guid Id { get; set; }
        public string Name { get; set; }

        public string Slug { get; set; }

        public string Description { get; set; }
        public string LogoUrl { get; set; }

        public string defaultCurrency { get; set; } = "USD";    


        public string Website { get; set; }
        public string StripeAccountId { get; set; }
        public bool IsActive { get; set; } = true;

        public bool IsDeleted { get; set; } = false;

        public bool IsUpdated { get; set; } = false;



        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;


        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
