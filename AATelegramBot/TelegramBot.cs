using AATelegramBot.DB.Entities;
using AATelegramBot.DB.Interfaces;
using AATelegramBot.Ftp;
using AATelegramBot.Models;
using AutoMapper;
using System.Collections.Concurrent;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace AATelegramBot
{
    public class TelegramBot
    {
        private static ITelegramBotClient _botClient;
        private static ReceiverOptions _receiverOptions;
        private static ConcurrentDictionary<long, UserRegistrationState> _registrationStates = new ConcurrentDictionary<long, UserRegistrationState>();
        private static ConcurrentDictionary<long, bool> _changePrefixes = new ConcurrentDictionary<long, bool>();
        private static List<string> _admins;

        private readonly IUserRepository _userRepository;
        private readonly IFtpService _ftpService;
        private readonly IMapper _mapper;

        public TelegramBot(IUserRepository userRepository, IFtpService ftpService, IMapper mapper)
        {
            _userRepository = userRepository;
            _ftpService = ftpService;
            _mapper = mapper;
        }

        public async Task Start(string token, List<string> admins)
        {
            _admins = admins;
            _botClient = new TelegramBotClient(token);

            _receiverOptions = new ReceiverOptions
            {
                AllowedUpdates = new[]
                {
                    UpdateType.Message,
                    UpdateType.CallbackQuery
                },
                ThrowPendingUpdates = true,
            };

            using var cts = new CancellationTokenSource();
            _botClient.StartReceiving(UpdateHandler, ErrorHandler, _receiverOptions, cts.Token);

            var me = await _botClient.GetMeAsync();
            Console.WriteLine($"{me.FirstName} запущен!");

            await Task.Delay(-1);
        }
        private async Task UpdateHandler(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            try
            {
                switch (update.Type)
                {
                    case UpdateType.Message:
                        {
                            var message = update.Message;
                            var user = message.From;
                            Console.WriteLine($"{user.FirstName} ({user.Id}) написал сообщение: {message.Text}");

                            if (message.Text == "/start" || message.Text == "/stop" || message.Text == "/profile")
                            {
                                if (_registrationStates.ContainsKey(message.Chat.Id))
                                {
                                    _registrationStates.TryRemove(message.Chat.Id, out var registrationState);
                                }
                                if (_changePrefixes.ContainsKey(message.Chat.Id))
                                {
                                    _changePrefixes.TryRemove(message.Chat.Id, out bool value);
                                }
                                await HandleStartCommand(message);
                            }

                            if (_registrationStates.ContainsKey(message.Chat.Id))
                            {
                                await HandleUserRegistrationStep(message);
                                return;
                            }

                            if (_changePrefixes.ContainsKey(message.Chat.Id))
                            {
                                await HandleConfirmChangePrefix(message);
                                return;
                            }


                            if (_admins.Contains(user.Username))
                            {
                                if (message.Text == "/admin")
                                {
                                    var messageText = $"Команды для админов:" +
                                        $"\nПолучить список пользователей /users" +
                                        $"\nПолучить пользователя по username /user \"username\"";
                                    await _botClient.SendTextMessageAsync(message.Chat.Id, messageText);
                                }
                                else if (message.Text == "/users")
                                {
                                    await HandleAdminGetUsersCommand(message);
                                }
                                else if (message.Text.StartsWith("/user"))
                                {
                                    await HandleAdminGetUserCommand(message);
                                }
                            }

                            return;
                        }
                    case UpdateType.CallbackQuery:
                        {
                            var callbackQuery = update.CallbackQuery;
                            await HandleCallbackQuery(callbackQuery);
                            return;
                        }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
        private Task ErrorHandler(ITelegramBotClient botClient, Exception error, CancellationToken cancellationToken)
        {
            var ErrorMessage = error switch
            {
                ApiRequestException apiRequestException
                    => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
                _ => error.Message
            };

            Console.WriteLine(ErrorMessage);
            return Task.CompletedTask;
        }
        private async Task HandleStartCommand(Message message)
        {
            var chatId = message.Chat.Id;
            var customUser = await _userRepository.GetUserByIdAsync(message.From.Id);

            if (customUser is null)
            {
                var buttons = new List<InlineKeyboardButton[]>
                {
                    new[] { InlineKeyboardButton.WithCallbackData("Регистрация", "register") }
                };

                await _botClient.SendTextMessageAsync(
                    chatId: chatId,
                    text: "Вы не зарегистрированы в системе. Нажмите на кнопку, чтобы пройти регистрацию",
                    replyMarkup: new InlineKeyboardMarkup(buttons)
                );
            }
            else
            {
                var payInfo = customUser.Data.IsPaid ? $"Оплачен до {customUser.Data?.EndTime}" : "Не оплачен";
                var messageText = $"Вы зарегистрированы в системе."
                    + $"\nТип привязки: {customUser.Data.BindType}"
                    + $"\nДанные для привязки: {customUser.Data.BindData}"
                    + $"\nПрефикс: {customUser.Data.Prefix}"
                    + $"\nСтатус: {payInfo}";

                var buttons = new List<InlineKeyboardButton[]>
                {
                    new[]
                    {
                        InlineKeyboardButton.WithCallbackData("Изменить все данные", "register"),
                        InlineKeyboardButton.WithCallbackData("Изменить префикс", $"update_prefix:{chatId}"),
                        InlineKeyboardButton.WithCallbackData("Связаться для оплаты", $"contacts:{chatId}")
                    }
                };

                await _botClient.SendTextMessageAsync(
                    chatId: chatId,
                    text: messageText,
                    replyMarkup: new InlineKeyboardMarkup(buttons)
                );
            }
        }

        private async Task HandleAdminGetUsersCommand(Message message)
        {
            var users = await _userRepository.GetUsersAsync();

            var messageText = "Список пользователей:";
            foreach (var user in users)
            {
                var payInfo = user.Data.IsPaid ? $"Оплачен до {user.Data?.EndTime}" : "Не оплачен";
                messageText += $"\n@{user.Username} - {payInfo}";
            }

            await _botClient.SendTextMessageAsync(message.Chat.Id, messageText);
        }
        private async Task HandleAdminGetUserCommand(Message message)
        {
            var username = message.Text.Remove(0, 6).Trim().Trim('\"');
            var user = await _userRepository.GetUserByNameAsync(username);

            if (user is null)
            {
                var messageText = $"Пользователь не найден.";
                await _botClient.SendTextMessageAsync(message.Chat.Id, messageText);
            }
            else
            {
                var payInfo = user.Data.IsPaid ? $"Оплачен до {user.Data?.EndTime}" : "Не оплачен";
                var messageText = $"Пользователь: @{user.Username}"
                    + $"\nТип привязки: {user.Data.BindType}"
                    + $"\nДанные для привязки: {user.Data.BindData}"
                    + $"\nПрефикс: {user.Data.Prefix}"
                    + $"\nСтатус: {payInfo}";

                var buttons = new List<InlineKeyboardButton>();
                buttons.Add(InlineKeyboardButton.WithCallbackData("Выдать префикс", $"mark_paid:{user.TelegramUserId}"));
                buttons.Add(InlineKeyboardButton.WithCallbackData("Удалить префикс", $"mark_nopaid:{user.TelegramUserId}"));

                await _botClient.SendTextMessageAsync(message.Chat.Id, messageText, replyMarkup: new InlineKeyboardMarkup(buttons));
            }
        }
        private async Task HandleCallbackQuery(CallbackQuery callbackQuery)
        {
            var data = callbackQuery.Data;
            var parts = data.Split(':');
            var action = parts[0];
            var userId = parts.Length > 1 ? long.Parse(parts[1]) : (long?)null;

            switch (action)
            {
                case "mark_paid":
                    await MarkUserAsPaid(callbackQuery, userId);
                    return;
                case "mark_nopaid":
                    await MarkUserAsNoPaid(callbackQuery, userId);
                    return;
                case "register":
                    await StartUserRegistration(callbackQuery.Message.Chat.Id);
                    break;
                case "update_prefix":
                    await UpdateUserPrefix(callbackQuery.Message.Chat.Id);
                    break;
                case "contacts":
                    await GetAdminInfo(callbackQuery.Message.Chat.Id);
                    break;
                case "bindtype_ip":
                    {
                        var state = _registrationStates[callbackQuery.Message.Chat.Id];
                        if(state != null)
                        {
                            state.UserData.BindType = "ip";
                            state.State = "BindData";
                            await _botClient.SendTextMessageAsync(callbackQuery.Message.Chat.Id, "Введите ip для привязки:");
                        }
                        break;
                    }
                case "bindtype_steamid":
                    {
                        var state = _registrationStates[callbackQuery.Message.Chat.Id];
                        if (state != null)
                        {
                            state.UserData.BindType = "steamid";
                            state.State = "BindData";
                            await _botClient.SendTextMessageAsync(callbackQuery.Message.Chat.Id, "Введите steamid для привязки:");
                        }
                        break;
                    }
                case "bindtype_nick":
                    {
                        var state = _registrationStates[callbackQuery.Message.Chat.Id];
                        if (state != null)
                        {
                            state.UserData.BindType = "nick";
                            state.State = "BindData";
                            await _botClient.SendTextMessageAsync(callbackQuery.Message.Chat.Id, "Введите nick для привязки:");
                        }
                        break;
                    }
            }
        }
        private async Task GetAdminInfo(long chatId)
        {
            var admin = _admins.Count > 0 ? _admins[0] : "Администраторов нет.";
            await _botClient.SendTextMessageAsync(
                chatId: chatId,
                text: $"Связь для оплаты: @{admin}"
            );
        }
        private async Task UpdateUserPrefix(long chatId)
        {
            _changePrefixes.TryAdd(chatId, true);

            await _botClient.SendTextMessageAsync(
                chatId: chatId,
                text: "Введите префикс для изменения:"
            );
        }
        private async Task HandleConfirmChangePrefix(Message message)
        {
            var chatId = message.Chat.Id;
            var messageText = message.Text;

            if (messageText.Length < 1 || messageText.Length > 32)
            {
                await _botClient.SendTextMessageAsync(chatId, "Ошибка. Префикс должен быть не меньше 1 и не больше 32 символов. Введите данные заново.");
                return;
            }

            _registrationStates.TryRemove(chatId, out var registrationState);

            var user = await _userRepository.GetUserByIdAsync(message.From.Id);

            if (user is null)
            {
                await _botClient.SendTextMessageAsync(chatId, "Пользователь не найден.");
            }
            else
            {
                try
                {
                    user.Data.Prefix = messageText;
                    await _userRepository.UpdateUser(user);
                    _changePrefixes.TryRemove(chatId, out bool value);

                    if (user.Data.IsPaid)
                    {
                        await _ftpService.SetOrUpdatePrefixInFile(user.Data);
                        await _botClient.SendTextMessageAsync(chatId, "Префикс успешно изменен, и загружен на сервер.");
                    }
                    else
                    {
                        await _botClient.SendTextMessageAsync(chatId, "Префикс успешно изменен.");
                    }

                }
                catch (Exception ex)
                {
                    await _botClient.SendTextMessageAsync(message.Chat.Id, ex.Message);
                }
            }
        }
        private async Task StartUserRegistration(long chatId)
        {
            _registrationStates.TryAdd(chatId, new UserRegistrationState
            {
                UserId = chatId,
                State = "BindType"
            });

            var buttons = new List<InlineKeyboardButton[]>
                {
                    new[]
                    {
                        InlineKeyboardButton.WithCallbackData("ip", $"bindtype_ip:{chatId}"),
                        InlineKeyboardButton.WithCallbackData("steamid", $"bindtype_steamid:{chatId}"),
                        InlineKeyboardButton.WithCallbackData("nick", $"bindtype_nick:{chatId}")
                    }
                };

            await _botClient.SendTextMessageAsync(
                chatId: chatId,
                text: "Выберите тип привязки:",
                replyMarkup: new InlineKeyboardMarkup(buttons)
            );
        }
        private async Task HandleUserRegistrationStep(Message message)
        {
            var chatId = message.Chat.Id;
            var messageText = message.Text;
            var state = _registrationStates[chatId];

            switch (state.State)
            {
                case "BindType":
                    var buttons = new List<InlineKeyboardButton[]>
                        {
                            new[]
                            {
                                InlineKeyboardButton.WithCallbackData("ip", $"bindtype_ip:{chatId}"),
                                InlineKeyboardButton.WithCallbackData("steamid", $"bindtype_steamid:{chatId}"),
                                InlineKeyboardButton.WithCallbackData("nick", $"bindtype_nick:{chatId}")
                            }
                        };

                    await _botClient.SendTextMessageAsync(
                        chatId: chatId,
                        text: "Выберите один из типов привязки (ip, steamid, nick):",
                        replyMarkup: new InlineKeyboardMarkup(buttons)
                    ); 
                    break;
                case "BindData":
                    if (messageText.Length < 1 || messageText.Length > 32)
                    {
                        await _botClient.SendTextMessageAsync(chatId, "Ошибка. Данные для привязки должен быть не меньше 1 и не больше 32 символов. Введите данные заново.");
                        return;
                    }
                    state.UserData.BindData = messageText;
                    state.State = "Prefix";
                    await _botClient.SendTextMessageAsync(chatId, "Введите префикс:");
                    break;
                case "Prefix":
                    if (messageText.Length < 1 || messageText.Length > 32)
                    {
                        await _botClient.SendTextMessageAsync(chatId, "Ошибка. Префикс должен быть не меньше 1 и не больше 32 символов. Введите данные заново.");
                        return;
                    }
                    state.UserData.Prefix = messageText;
                    _registrationStates.TryRemove(chatId, out var registrationState);
                    var user = await _userRepository.GetUserByIdAsync(message.From.Id);
                    if (user is null)
                    {
                        var customUser = _mapper.Map<CustomUser>(message.From);
                        customUser.Data = state.UserData;

                        await _userRepository.CreateUserAsync(customUser);
                        await _botClient.SendTextMessageAsync(chatId, "Регистрация прошла успешно.");
                    }
                    else
                    {
                        user.Data.BindType = state.UserData.BindType;
                        user.Data.BindData = state.UserData.BindData;
                        user.Data.Prefix = state.UserData.Prefix;

                        await _userRepository.UpdateUser(user);

                        try
                        {
                            if (user.Data.IsPaid)
                            {
                                await _ftpService.SetOrUpdatePrefixInFile(user.Data);
                                await _botClient.SendTextMessageAsync(chatId, "Данные успешно изменены, и загружены на сервер.");
                            }
                            else
                            {
                                await _botClient.SendTextMessageAsync(chatId, "Данные успешно изменены.");
                            }
                        }
                        catch (Exception ex)
                        {
                            await _botClient.SendTextMessageAsync(message.Chat.Id, ex.Message);
                        }
                    }
                    break;
            }
        }
        private async Task MarkUserAsPaid(CallbackQuery callbackQuery, long? userId)
        {
            var user = await _userRepository.GetUserByIdAsync(userId);
            if (user is null)
            {
                await _botClient.AnswerCallbackQueryAsync(callbackQuery.Id, "Пользователь не найден.");
            }
            else
            {
                try
                {
                    await _userRepository.SetUserEndTime(user);
                    var resultMessage = "";

                    var result = await _userRepository.SetUserAsPaidAsync(userId);
                    if (!result)
                    {
                        resultMessage += $"Пользователь @{user.Username} уже помечен как оплаченный.";
                    }

                    await _ftpService.SetOrUpdatePrefixInFile(user.Data);
                    resultMessage += $"Пользователь @{user.Username} добавлен/обновлен в prefixlist на сервере.";
                    await _botClient.AnswerCallbackQueryAsync(callbackQuery.Id, resultMessage);
                }
                catch (Exception ex)
                {
                    await _botClient.AnswerCallbackQueryAsync(callbackQuery.Id, ex.Message);
                }
            }
        }
        private async Task MarkUserAsNoPaid(CallbackQuery callbackQuery, long? userId)
        {
            var user = await _userRepository.GetUserByIdAsync(userId);

            if (user is null)
            {
                await _botClient.AnswerCallbackQueryAsync(callbackQuery.Id, "Пользователь не найден.");
            }
            else
            {
                try
                {
                    var result = await _userRepository.SetUserAsNoPaidAsync(userId);
                    var resultDelete = await _ftpService.DeletePrefixFromFile(user.Data);
                    var resultMessage = "";

                    if (result && resultDelete)
                    {
                        resultMessage += $"Пользователь @{user.Username} помечен как не оплачен и удален из prefixlist на сервере.";
                    }
                    if (!result)
                    {
                        resultMessage += $"Пользователь @{user.Username} уже помечен как не оплаченный. ";
                    }
                    if (!resultDelete)
                    {
                        resultMessage += $"Пользователя @{user.Username} нет в prefixlist на сервере.";
                    }

                    await _botClient.AnswerCallbackQueryAsync(callbackQuery.Id, resultMessage);
                }
                catch (Exception ex)
                {
                    await _botClient.AnswerCallbackQueryAsync(callbackQuery.Id, ex.Message);
                }
            }
        }
    }
}