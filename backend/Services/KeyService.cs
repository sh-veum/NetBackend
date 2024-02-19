using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetBackend.Constants;
using NetBackend.Models.Keys;
using NetBackend.Models.User;
using NetBackend.Services.Interfaces;
using NetBackend.Tools;

namespace NetBackend.Services;

public partial class KeyService : IKeyService
{
    private readonly ILogger<KeyService> _logger;
    private readonly ICryptoService _cryptoService;
    private readonly IDbContextService _dbContextService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public KeyService(
        ILogger<KeyService> logger,
        ICryptoService cryptoService,
        IDbContextService dbContextService,
        IHttpContextAccessor httpContextAccessor)
    {
        _logger = logger;
        _cryptoService = cryptoService;
        _dbContextService = dbContextService;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<string> EncryptAndStoreAccessKey(IApiKey apiKey, UserModel user)
    {
        var dbContext = await _dbContextService.GetUserDatabaseContext(user);
        var dataToEncrypt = $"Id:{apiKey.Id},Type:{apiKey.GetType().Name}";
        var encryptedKey = _cryptoService.Encrypt(dataToEncrypt, SecretConstants.SecretKey);

        var accessKey = new AccessKey
        {
            KeyHash = ComputeHash.ComputeSha256Hash(encryptedKey)
        };

        dbContext.Set<AccessKey>().Add(accessKey);
        await dbContext.SaveChangesAsync();

        return encryptedKey;
    }

    public async Task<(IApiKey?, IActionResult?)> DecryptAccessKey(string encryptedKey)
    {
        var decryptedData = _cryptoService.Decrypt(encryptedKey, SecretConstants.SecretKey);
        var dataParts = decryptedData.Split(',');

        var typePart = dataParts.FirstOrDefault(part => part.StartsWith("Type:"))?.Split(':')[1];
        var idPart = dataParts.FirstOrDefault(part => part.StartsWith("Id:"))?.Split(':')[1];

        if (typePart == null || idPart == null || !int.TryParse(idPart, out var id))
        {
            return (null, new BadRequestObjectResult("Invalid encrypted key format."));
        }

        var dbContext = await _dbContextService.GetDatabaseContextByName(DatabaseConstants.MainDbName);
        return await FetchApiKeyAsync(typePart, id, dbContext);
    }

    public async Task<(IApiKey?, IActionResult?)> DecryptAccessKeyUserCheck(string encryptedKey, string currentUserId)
    {
        var (apiKey, result) = await DecryptAccessKey(encryptedKey);
        if (result != null)
        {
            return (null, result);
        }

        if (apiKey?.UserId != currentUserId)
        {
            return (null, new UnauthorizedResult());
        }

        return (apiKey, null);
    }

    public async Task<(DbContext? dbContext, IActionResult? actionResult)> ProcessAccessKey(string encryptedKey)
    {
        var (apiKey, errorResult) = await DecryptAccessKey(encryptedKey);
        if (errorResult != null) return (null, errorResult);

        if (apiKey == null)
        {
            return (null, new BadRequestObjectResult("API key not found."));
        }

        var expirationDate = apiKey.CreatedAt.AddDays(apiKey.ExpiresIn);
        if (DateTime.UtcNow > expirationDate)
        {
            return (null, new UnauthorizedResult());
        }

        var httpContext = _httpContextAccessor.HttpContext;
        if (apiKey is ApiKey api)
        {
            if (!string.IsNullOrEmpty(httpContext?.Request.Path.Value) &&
                api.AccessibleEndpoints != null &&
                !api.AccessibleEndpoints.Contains(httpContext.Request.Path.Value))
            {
                return (null, new UnauthorizedResult());
            }
        }

        if (apiKey.UserId == null) return (null, new BadRequestObjectResult("User ID not found in the access key."));

        var mainDbContext = await _dbContextService.GetDatabaseContextByName(DatabaseConstants.MainDbName);
        string databaseName = mainDbContext.Set<UserModel>().FirstOrDefault(u => u.Id == apiKey.UserId)?.DatabaseName ?? "";

        var selectedContext = await _dbContextService.GetDatabaseContextByName(databaseName);

        // Compute hash of the encrypted key and check if it exists in the database
        var keyHash = ComputeHash.ComputeSha256Hash(encryptedKey);
        var accessKey = await selectedContext.Set<AccessKey>().FirstOrDefaultAsync(ak => ak.KeyHash == keyHash);
        if (accessKey == null)
        {
            return (null, new UnauthorizedResult());
        }

        return (selectedContext, null);
    }

    public async Task<(DbContext? dbContext, IActionResult? actionResult)> ProcessGraphQLAccessKey(string encryptedKey)
    {
        var (apiKey, errorResult) = await DecryptAccessKey(encryptedKey);
        if (errorResult != null) return (null, errorResult);

        if (apiKey == null)
        {
            return (null, new BadRequestObjectResult("API key not found."));
        }

        var expirationDate = apiKey.CreatedAt.AddDays(apiKey.ExpiresIn);
        if (DateTime.UtcNow > expirationDate)
        {
            return (null, new UnauthorizedResult());
        }

        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
        {
            return (null, new BadRequestObjectResult("HttpContext is null."));
        }

        // Retrieve the stored GraphQL query from HttpContext
        var graphqlQuery = httpContext.Items["GraphQLQuery"] as string;

        _logger.LogInformation($"Retrieved GraphQL query from HttpContext: {graphqlQuery}");

        if (string.IsNullOrEmpty(graphqlQuery))
        {
            return (null, new UnauthorizedResult()); // No query to authorize
        }

        if (apiKey is GraphQLApiKey api)
        {
            var permission = await GetAccessKeyPermission(apiKey.Id);
            var isAuthorized = CheckQueryAuthorization(graphqlQuery, permission);
            if (!isAuthorized)
            {
                return (null, new UnauthorizedResult());
            }
        }

        if (apiKey.UserId == null) return (null, new BadRequestObjectResult("User ID not found in the access key."));

        var mainDbContext = await _dbContextService.GetDatabaseContextByName(DatabaseConstants.MainDbName);
        string databaseName = mainDbContext.Set<UserModel>().FirstOrDefault(u => u.Id == apiKey.UserId)?.DatabaseName ?? "";

        var selectedContext = await _dbContextService.GetDatabaseContextByName(databaseName);

        // Compute hash of the encrypted key and check if it exists in the database
        var keyHash = ComputeHash.ComputeSha256Hash(encryptedKey);
        var accessKey = await selectedContext.Set<AccessKey>().FirstOrDefaultAsync(ak => ak.KeyHash == keyHash);
        if (accessKey == null)
        {
            return (null, new UnauthorizedResult());
        }

        return (selectedContext, null);
    }

    public async Task<IActionResult> RemoveAccessKey(string encryptedKey)
    {
        var (apiKey, actionResult) = await DecryptAccessKey(encryptedKey);
        if (actionResult != null)
        {
            return actionResult;
        }

        var dbContext = await _dbContextService.GetDatabaseContextByName(DatabaseConstants.MainDbName);
        if (apiKey != null)
        {
            RemoveApiKey(apiKey, dbContext);
            await dbContext.SaveChangesAsync();
        }

        return new OkResult();
    }

    private static async Task<(IApiKey?, IActionResult?)> FetchApiKeyAsync(string typePart, int id, DbContext dbContext)
    {
        if (dbContext == null)
        {
            return (null, new NotFoundObjectResult("Database context not found."));
        }

        switch (typePart.ToLowerInvariant())
        {
            case "apikey":
                var apiKey = await dbContext.Set<ApiKey>().Include(a => a.User).FirstOrDefaultAsync(a => a.Id == id);
                return apiKey == null ? (null, new NotFoundObjectResult("Api Key not found.")) : (apiKey, null);
            case "graphqlapikey":
                var graphQLApiKey = await dbContext.Set<GraphQLApiKey>().Include(a => a.User).FirstOrDefaultAsync(a => a.Id == id);
                return graphQLApiKey == null ? (null, new NotFoundObjectResult("GraphQL Api Key not found.")) : (graphQLApiKey, null);
            default:
                return (null, new BadRequestObjectResult($"Unknown key type: {typePart}."));
        }
    }

    private static void RemoveApiKey(IApiKey apiKey, DbContext dbContext)
    {
        switch (apiKey)
        {
            case ApiKey api:
                dbContext.Set<ApiKey>().Remove(api);
                break;
            case GraphQLApiKey gqlApi:
                dbContext.Set<GraphQLApiKey>().Remove(gqlApi);
                break;
        }
    }

    private bool CheckQueryAuthorization(string graphqlQuery, List<AccessKeyPermission> permissions)
    {
        var parsedQuery = GraphQLQueryParser.ParseQuery(graphqlQuery);

        if (parsedQuery.Count == 0)
        {
            _logger.LogWarning("Parsed query is empty. Authorization check failed.");
            return false;
        }

        _logger.LogInformation("Starting authorization check for GraphQL query.");

        foreach (var operation in parsedQuery)
        {
            var operationName = operation.Key.ToLowerInvariant();
            var requestedFields = operation.Value.Select(f => f.ToLowerInvariant()).ToList();

            _logger.LogInformation($"Checking operation: {operation.Key} with fields: {string.Join(", ", operation.Value)}");

            var permission = permissions.FirstOrDefault(p => p.QueryName.Equals(operationName, StringComparison.OrdinalIgnoreCase));

            if (permission == null)
            {
                _logger.LogWarning($"Unauthorized operation: {operation.Key}. No matching permission found.");
                return false;
            }

            var allowedFields = permission.AllowedFields?.Select(f => f.ToLowerInvariant()).ToList() ?? [];

            _logger.LogInformation($"Allowed fields for operation '{operation.Key}': {string.Join(", ", allowedFields)}");

            foreach (var field in requestedFields)
            {
                if (!allowedFields.Contains(field))
                {
                    _logger.LogWarning($"Unauthorized field: {field} in operation: {operation.Key}. Field is not in the allowed list.");
                    return false;
                }
            }
        }

        _logger.LogInformation("GraphQL query authorization check passed.");

        return true; // All requested operations and fields are allowed
    }

    private async Task<List<AccessKeyPermission>> GetAccessKeyPermission(int graphQLApiKeyId)
    {
        var mainDbContext = await _dbContextService.GetDatabaseContextByName(DatabaseConstants.MainDbName);

        var accessKeyPermissions = await mainDbContext.Set<AccessKeyPermission>()
            .Where(p => p.GraphQLApiKeyId == graphQLApiKeyId)
            .ToListAsync();

        return accessKeyPermissions;
    }
}

