using KuchniaUCygana.Application.DTOs;
using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Domain.Entities.Auth;
using KuchniaUCygana.Domain.Interfaces;
using KuchniaUCygana.Infrastructure.ExternalServices.Maps;

namespace KuchniaUCygana.Application.Services.Logistics;

/// <summary>
/// Pobiera dane, przesyła do API i zleca zapis
/// </summary>

//używa IAddressRepository i IGeocodeService
public class GeocodingOrchestrator : IGeocodeService
{
}