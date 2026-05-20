using AutoMapper;
using KuchniaUCygana.Application.DTOs;
using KuchniaUCygana.Application.DTOs.Menu;
using KuchniaUCygana.Domain.Entities.Auth;

namespace KuchniaUCygana.Application.Mappings;

public sealed class UserProfile : Profile
{
    public UserProfile()
    {
        CreateMap<User, UserDto>()
            .ForMember(
                destination => destination.FullName,
                options => options.MapFrom(source => $"{source.FirstName} {source.LastName}"));
    }
}
