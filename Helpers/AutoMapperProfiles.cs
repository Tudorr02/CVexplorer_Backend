using AutoMapper;
using CVexplorer.Models.Domain;
using CVexplorer.Models.DTO;

namespace CVexplorer.Helpers
{
    public class AutoMapperProfiles : Profile
    {
        public AutoMapperProfiles()
        {
            CreateMap<User, UserDTO>();
            CreateMap<RegisterDTO, User>();
            CreateMap<LoginDTO, User>();
        }
    }
}
