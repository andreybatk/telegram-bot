using AATelegramBot.DB.Entities;
using AATelegramBot.DB.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AATelegramBot.DB.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly ApplicationDbContext _context;

        public UserRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<bool> SetUserAsPaidAsync(long? id)
        {
            var user = await _context.Users.FirstOrDefaultAsync(x => x.TelegramUserId == id);

            if (user is null)
            {
                throw new ArgumentNullException($"{nameof(id)} is null.");
            }

            if (user.Data.IsPaid)
            {
                return false;
            }

            user.Data.IsPaid = true;
            await _context.SaveChangesAsync();
            return true;
        }
        public async Task<bool> SetUserAsNoPaidAsync(long? id)
        {
            var user = await _context.Users.FirstOrDefaultAsync(x => x.TelegramUserId == id);

            if (user is null)
            {
                throw new ArgumentNullException($"{nameof(id)} is null.");
            }

            if(!user.Data.IsPaid)
            {
                return false;
            }

            user.Data.IsPaid = false;
            await _context.SaveChangesAsync();
            return true;
        }
        public async Task<IEnumerable<CustomUser>> GetUsersAsync()
        {
            return await _context.Users.ToListAsync();
        }
        public async Task<IEnumerable<CustomUser>> GetPaidUsersAsync()
        {
            return await _context.Users.Where(x => x.Data.IsPaid).ToListAsync();
        }
        public async Task<CustomUser?> GetUserByNameAsync(string username)
        {
            return await _context.Users.FirstOrDefaultAsync(x => x.Username == username);
        }
        public async Task<CustomUser?> GetUserByIdAsync(long? id)
        {
            return await _context.Users.FirstOrDefaultAsync(x => x.TelegramUserId == id);
        }
        public async Task UpdateUser(CustomUser user)
        {
            _context.Users.Update(user);
            await _context.SaveChangesAsync();
        }
        public async Task SetUserEndTime(CustomUser user)
        {
            user.Data.EndTime = DateTime.Now.AddDays(30);
            _context.Users.Update(user);
            await _context.SaveChangesAsync();
        }
        public async Task CreateUserAsync(CustomUser user)
        {
            await _context.Users.AddAsync(user);
            await _context.SaveChangesAsync();
        }
    }
}