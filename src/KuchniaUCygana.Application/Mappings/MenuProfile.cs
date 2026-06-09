using AutoMapper;
using KuchniaUCygana.Application.DTOs.Menu;
using KuchniaUCygana.Domain.Entities.Menu;

namespace KuchniaUCygana.Application.Mappings;

public sealed class MenuProfile : Profile
{
    public MenuProfile()
    {
        this.CreateMap<Diet, DietDto>()
            .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status.ToString()));
        this.CreateMap<DietVariant, DietVariantDto>();
        this.CreateMap<Meal, MealDto>()
            .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status.ToString()));
        this.CreateMap<Meal, MealDetailDto>()
            .IncludeBase<Meal, MealDto>()
            .ForMember(dest => dest.Recipe, opt => opt.Ignore())
            .ForMember(dest => dest.Components, opt => opt.Ignore())
            .ForMember(dest => dest.Images, opt => opt.Ignore());
        this.CreateMap<Ingredient, IngredientDto>().ReverseMap();
        this.CreateMap<Allergen, AllergenDto>().ReverseMap();
        this.CreateMap<Recipe, RecipeItemDto>()
            .ForMember(dest => dest.IngredientName, opt => opt.Ignore());
        this.CreateMap<NutritionFact, NutritionFactDto>();
        this.CreateMap<MealImage, MealImageDto>();
        this.CreateMap<CreateMealRequest, Meal>();
        this.CreateMap<UpdateMealRequest, Meal>();
        this.CreateMap<CreateDietRequest, Diet>();
        this.CreateMap<UpdateDietRequest, Diet>();
        this.CreateMap<CreateDietVariantRequest, DietVariant>();
    }
}
