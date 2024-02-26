using NetBackend.Models;
using NetBackend.Models.Dto;
using NetBackend.Models.Dto.Keys;

namespace NetBackend.Constants;

public static class ApiConstants
{
    public static readonly List<ApiEndpointDto> DefaultApiEndpoints =
    [
        new() {
            Path = "/api/aquaculturelist/fishhealth/licenseelist",
            Method = "POST",
            ExpectedBodyType = typeof(OrganizationDto)
        },
        new() {
            Path = "/api/aquaculturelist/fishhealth/species",
            Method = "POST",
            ExpectedBodyType = typeof(SpeciesDto)
        }
    ];
}