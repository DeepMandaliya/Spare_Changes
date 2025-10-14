using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace The_Charity.Models
{
    public class PlatformAdmin
    {
        [Key]
        public Guid Id { get; set; } // Changed to Guid

        [Required]
        [ForeignKey("User")]
        public Guid UserId { get; set; } // Changed to Guid to match User.Id

        public virtual User User { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
