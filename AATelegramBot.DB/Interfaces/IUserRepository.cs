using AATelegramBot.DB.Entities;

namespace AATelegramBot.DB.Interfaces
{
    public interface IUserRepository
    {
        Task<IEnumerable<CustomUser>> GetUsersAsync();
        Task<IEnumerable<CustomUser>> GetPaidUsersAsync();
        Task<CustomUser?> GetUserByNameAsync(string username);
        Task<CustomUser?> GetUserByIdAsync(long? id);
        Task<bool> SetUserAsPaidAsync(long? id);
        Task<bool> SetUserAsNoPaidAsync(long? id);
        Task CreateUserAsync(CustomUser user);
        Task UpdateUser(CustomUser user);
        Task SetUserEndTime(CustomUser user);
    }
}