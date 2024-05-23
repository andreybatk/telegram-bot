# AATelegramBot
Telegram Bot Prefix Manager. Для взаимодействия с плагином chatmanager (amxx). 
Настройки подключениий в файле appsettings.json проекта AATelegramBot.
Бот позволяет добавлять префиксы игрокам, загружая их на ftp, для этого им нужно зарегистрироваться в боте. Админ может выдать префикс по нажатию кнопки, или удалить его.
## Технологии
- ASP NET CORE MVC (.NET 7)
- Entity Framework Core (7.0.19)
- MSSQL
- FluentFTP
- AutoMapper
- Telegram.Bot

## Слои
- AATelegramBot - Console App
- AATelegramBot.DB - Data Access
