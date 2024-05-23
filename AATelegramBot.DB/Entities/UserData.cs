using System.ComponentModel.DataAnnotations.Schema;

namespace AATelegramBot.DB.Entities
{
    public class UserData
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long UserDataId { get; set; }
        public bool IsPaid { get; set; }
        public string BindType { get; set; }
        public string BindData { get; set; }
        public string Prefix { get; set; }
        public DateTime? EndTime { get; set; }
        public virtual CustomUser CustomUser { get; set; }
    }
}