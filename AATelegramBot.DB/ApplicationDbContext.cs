using AATelegramBot.DB.Entities;
using Microsoft.EntityFrameworkCore;

namespace AATelegramBot.DB
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
            Database.EnsureCreated();
        }

        public DbSet<CustomUser> Users { get; set; }
        public DbSet<UserData> UsersData { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CustomUser>()
             .HasOne(cu => cu.Data)
             .WithOne(ud => ud.CustomUser)
             .HasForeignKey<CustomUser>(cu => cu.UserDataId);
        }
    }
}