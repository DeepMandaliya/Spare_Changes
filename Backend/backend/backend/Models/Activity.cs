using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace The_Charity.Models
{
    public class Activity
    {
        [Key]
        public Guid Id { get; set; }

        public Guid? CharityId { get; set; }

        [ForeignKey("CharityId")]
        public virtual Charity Charity { get; set; }

        public Guid? UserId { get; set; }

        [ForeignKey("UserId")]
        public virtual User User { get; set; }

        [Required]
        [StringLength(100)]
        public string Type { get; set; }

        [StringLength(1000)]
        public string Summary { get; set; }

        public string DataJson { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
