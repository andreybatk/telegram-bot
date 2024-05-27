using AATelegramBot.DB.Entities;
using AATelegramBot.Models;
using FluentFTP;
using FluentFTP.Helpers;
using Microsoft.Extensions.Configuration;
using System;
using System.Globalization;
using System.Text;

namespace AATelegramBot.Ftp
{
    public class FtpService : IFtpService
    {
        private static string _host;
        private static string _username;
        private static string _password;
        private static string _remotePath;
        private static string _localPath;

        private static SemaphoreSlim semaphore;

        static FtpService()
        {
            var builder = new ConfigurationBuilder()
                 .AddJsonFile($"appsettings.json", true, true);
            var config = builder.Build();

            _host = config["FtpConnection:Host"] ?? throw new InvalidOperationException("FtpConnection:Host is null!");
            _username = config["FtpConnection:UserName"] ?? throw new InvalidOperationException("FtpConnection:UserName is null!");
            _password = config["FtpConnection:Password"] ?? throw new InvalidOperationException("FtpConnection:Password is null!");

            _remotePath = config["FtpRemoteFilePath"] ?? throw new InvalidOperationException("FtpRemoteFilePath is null!");
            _localPath = Path.GetFileName(_remotePath);

            semaphore = new SemaphoreSlim(1);
        }

        public async Task SetOrUpdatePrefixInFile(UserData? userData)
        {
            if (userData is null)
            {
                throw new ArgumentNullException($"{nameof(userData)} is null!");
            }

            await semaphore.WaitAsync();
            try
            {
                await DownloadFileAsync();
                await SetPrefix(userData);
                await UploadFileAsync();
            }
            finally
            {
                semaphore.Release();
            }
        }
        public async Task<bool> DeletePrefixFromFile(UserData? userData)
        {
            if (userData is null)
            {
                throw new ArgumentNullException($"{nameof(userData)} is null!");
            }
            bool result = false;

            await semaphore.WaitAsync();
            try
            {
                await DownloadFileAsync();
                result = await DeletePrefix(userData);
                if (result)
                {
                    await UploadFileAsync();
                }
            }
            finally
            {
                semaphore.Release();
            }

            return result;
        }
        private async Task DownloadFileAsync()
        {
            using var ftp = new AsyncFtpClient(_host, _username, _password);
            await ftp.Connect();

            var status = await ftp.DownloadFile(_localPath, _remotePath, FtpLocalExists.Overwrite);
            if (status.IsFailure()) throw new InvalidOperationException("DownloadFileAsync is failure!");
        }
        private async Task UploadFileAsync()
        {
            using var ftp = new AsyncFtpClient(_host, _username, _password);
            await ftp.Connect();

            var status = await ftp.UploadFile(_localPath, _remotePath, FtpRemoteExists.Overwrite, true);
            if (status.IsFailure()) throw new InvalidOperationException("UploadFileAsync is failure!");
        }
        private async Task SetPrefix(UserData userData)
        {
            var readerInfo = await GetUserReaderInfoFromFile(userData.UserDataId);
            var resultPrefix = CreateResultPrefix(userData);

            if (readerInfo.IsExist)
            {
                await DeleteLineFromFileAsync(readerInfo.LineIndex);
            }

            using var writer = new StreamWriter(_localPath, true, Encoding.UTF8);
            await writer.WriteLineAsync(resultPrefix);
        }
        private async Task<bool> DeletePrefix(UserData userData)
        {
            var readerInfo = await GetUserReaderInfoFromFile(userData.UserDataId);

            if (readerInfo.IsExist)
            {
                await DeleteLineFromFileAsync(readerInfo.LineIndex);
                return true;
            }

            return false;
        }
        private async Task<UserReaderInfo> GetUserReaderInfoFromFile(long id)
        {
            using var reader = new StreamReader(_localPath, Encoding.UTF8);
            bool isExist = false;
            int lineIndex = 0;
            string line;

            while ((line = await reader.ReadLineAsync()) != null)
            {
                if (line.StartsWith(';'))
                {
                    lineIndex++;
                    continue;
                }

                if (line.Contains($"[{id}]"))
                {
                    isExist = true;
                    return new UserReaderInfo { IsExist = isExist, LineIndex = lineIndex };
                }
                lineIndex++;
            }

            return new UserReaderInfo { IsExist = isExist };
        }
        private async Task DeleteLineFromFileAsync(int lineIndex)
        {
            string tempFilePath = _localPath + ".tmp";

            try
            {
                using (StreamReader reader = new StreamReader(_localPath, Encoding.UTF8))
                using (StreamWriter writer = new StreamWriter(tempFilePath, false, Encoding.UTF8))
                {
                    string line;
                    int currentLineIndex = 0;

                    while ((line = await reader.ReadLineAsync()) != null)
                    {
                        if (currentLineIndex != lineIndex)
                        {
                            await writer.WriteLineAsync(line);
                        }
                        currentLineIndex++;
                    }

                    if (lineIndex >= currentLineIndex)
                    {
                        throw new ArgumentOutOfRangeException(nameof(lineIndex), "Указанный индекс строки выходит за пределы диапазона.");
                    }
                }

                File.Delete(_localPath);
                File.Move(tempFilePath, _localPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);

                if (File.Exists(tempFilePath))
                {
                    File.Delete(tempFilePath);
                }
            }
        }
        private string CreateResultPrefix(UserData userData)
        {
            string formattedDateTime = userData.EndTime?.ToString(new CultureInfo("ru"));
            return $"[{userData.UserDataId}] \"{formattedDateTime}\" \"{userData.BindType}\" \"{userData.BindData}\" \"!g[{userData.Prefix}]!d\"";
        }
    }
}