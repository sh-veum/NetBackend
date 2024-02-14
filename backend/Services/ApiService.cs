using System.Reflection;
using NetBackend.Constants;
using NetBackend.Models.Keys;
using NetBackend.Models.User;
using NetBackend.Services.Interfaces;

namespace NetBackend.Services;

public class ApiService : IApiService
{
    private readonly ILogger<KeyService> _logger;
    private readonly IDbContextService _databaseContextService;

    public ApiService(
        ILogger<KeyService> logger,
        IDbContextService databaseContextService)
    {
        _logger = logger;
        _databaseContextService = databaseContextService;
    }

    public object GetDtoStructure(Type dtoType)
    {
        var properties = dtoType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(prop => new
            {
                Name = prop.Name,
                Type = GetFriendlyTypeName(prop.PropertyType)
            })
            .ToList();

        return new { Properties = properties };
    }

    public string GetFriendlyTypeName(Type type)
    {
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            return $"{GetFriendlyTypeName(type.GetGenericArguments()[0])}?";
        }
        else if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
        {
            return $"List<{GetFriendlyTypeName(type.GetGenericArguments()[0])}>";
        }
        else
        {
            // Return a simplified type name for common types
            switch (Type.GetTypeCode(type))
            {
                case TypeCode.Boolean: return "bool";
                case TypeCode.Int32: return "int";
                case TypeCode.String: return "string";
                default: return type.Name;
            }
        }
    }

    public async Task<ApiKey> CreateRESTApiKey(UserModel user, string keyName, List<string> endpoints)
    {
        var dbContext = await _databaseContextService.GetDatabaseContextByName(DatabaseConstants.MainDbName);

        // Create a new ApiKey instance
        var apiKey = new ApiKey
        {
            UserId = user.Id,
            KeyName = keyName,
            AccessibleEndpoints = endpoints,
            User = user,
            CreatedAt = DateTime.UtcNow,
            ExpiresIn = KeyConstants.ExpiresIn
        };

        // Add the new ApiKey to the DbContext
        dbContext.Set<ApiKey>().Add(apiKey);
        await dbContext.SaveChangesAsync();

        return apiKey;
    }


    // GraphQL
    public async Task<GraphQLApiKey> CreateGraphQLApiKey(UserModel user, string keyName, List<string> allowedQueries)
    {
        var dbContext = await _databaseContextService.GetDatabaseContextByName(DatabaseConstants.MainDbName);

        var graphQLApiKey = new GraphQLApiKey
        {
            KeyName = keyName,
            UserId = user.Id,
            User = user,
            AllowedQueries = allowedQueries,
            CreatedAt = DateTime.UtcNow,
            ExpiresIn = KeyConstants.ExpiresIn
        };

        dbContext.Set<GraphQLApiKey>().Add(graphQLApiKey);
        await dbContext.SaveChangesAsync();

        return graphQLApiKey;
    }
}
