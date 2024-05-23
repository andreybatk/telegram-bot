using AATelegramBot.DB.Entities;

namespace AATelegramBot.Ftp
{
    public interface IFtpService
    {
        Task SetOrUpdatePrefixInFile(UserData? userData);
        Task<bool> DeletePrefixFromFile(UserData? userData);
    }
}