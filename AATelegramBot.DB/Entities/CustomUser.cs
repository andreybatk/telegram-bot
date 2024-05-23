using System.ComponentModel.DataAnnotations.Schema;

namespace AATelegramBot.DB.Entities
{
    public class CustomUser
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long CustomUserId { get; set; }
        public long TelegramUserId { get; set; }
        public long UserDataId { get; set; }
        [ForeignKey("UserDataId")]
        public virtual UserData? Data { get; set; }
        public bool IsBot { get; set; }
        public string FirstName { get; set; } = default!;
        public string? LastName { get; set; }
        public string? Username { get; set; }
    }
}