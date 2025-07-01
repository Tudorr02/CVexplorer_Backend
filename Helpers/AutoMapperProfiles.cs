using AutoMapper;
using CVexplorer.Models.Domain;
using CVexplorer.Models.DTO;

namespace CVexplorer.Helpers
{
    public class AutoMapperProfiles : Profile
    {
        public AutoMapperProfiles()
        {
            CreateMap<User, AccountDTO>();
            CreateMap<UserEnrollDTO, User>()
                .ForMember(dest => dest.UserRoles, opt => opt.Ignore())
                .ForMember(dest => dest.CompanyId, opt => opt.Ignore())
                .ForMember(dest => dest.Company, opt => opt.Ignore());

            CreateMap<LoginDTO, User>();
            CreateMap<Position, PositionPayloadInputDTO>()
               .ForMember(dest => dest.Certifications,
                       opt => opt.MapFrom(src => src.Certification));

            CreateMap<Round, RoundListDTO>();
            CreateMap<RoundListDTO, Round>();
               
        }
    }
}
