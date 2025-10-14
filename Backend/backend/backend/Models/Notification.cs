using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace The_Charity.Models
{
    public class Notification
    {

        [Key]
        public Guid Id { get; set; }

        public Guid? UserId { get; set; }

        public Guid? CharityId { get; set; }

        public Guid? TemplateId { get; set; }

        public DateTime? SentAt { get; set; }

        [StringLength(50)]
        public string? Status { get; set; }

        public string? Payload { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        [ForeignKey("UserId")]
        public virtual User? User { get; set; }

        [ForeignKey("CharityId")]
        public virtual Charity? Charity { get; set; }

        [ForeignKey("TemplateId")]
        public virtual NotificationTemplate? Template { get; set; }
    }
}