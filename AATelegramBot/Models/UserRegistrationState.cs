using AATelegramBot.DB.Entities;

namespace AATelegramBot.Models
{
    public class UserRegistrationState
    {
        public long UserId { get; set; }
        public string State { get; set; }
        public UserData UserData { get; set; } = new UserData();
    }
}