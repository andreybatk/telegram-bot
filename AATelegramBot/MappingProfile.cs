using AATelegramBot.DB.Entities;
using AutoMapper;
using Telegram.Bot.Types;

namespace AATelegramBot
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            CreateMap<User, CustomUser>()
                .ForMember(dest => dest.UserDataId, opt => opt.Ignore())
                .ForMember(dest => dest.Data, opt => opt.Ignore())
                .ForMember(dest => dest.TelegramUserId, opt => opt.MapFrom(src => src.Id));
        }
    }
}