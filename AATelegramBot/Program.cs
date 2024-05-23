using AATelegramBot.DB;
using AATelegramBot.DB.Interfaces;
using AATelegramBot.DB.Repositories;
using AATelegramBot.Ftp;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AATelegramBot
{
    public class Program
    {
        public static async Task Main()
        {
            var configBuilder = new ConfigurationBuilder()
                 .AddJsonFile($"appsettings.json", true, true);
            var config = configBuilder.Build();

            var token = config["TelegramBotToken"] ?? throw new InvalidOperationException("TelegramBotToken is null!");
            var connection = config.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("DefaultConnection is null!");
            var admins = config.GetSection("admins:admin").Get<List<string>>();
            var services = new ServiceCollection()
                .AddDbContext<ApplicationDbContext>(options =>
                {
                    options.UseLazyLoadingProxies();
                    //options.UseMySql(connection, new MySqlServerVersion(new Version(8, 0, 11)));
                    options.UseSqlServer(connection);
                })
                .AddAutoMapper(typeof(MappingProfile))
                .AddSingleton<TelegramBot>()
                .AddScoped<IFtpService, FtpService>()
                .AddScoped<IUserRepository, UserRepository>()
                .BuildServiceProvider();

            await services.GetRequiredService<TelegramBot>().Start(token, admins);
        }
    }
}