using backend.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Name.Models.Dto;
using Netbackend.Models.Dto.Keys;
using NetBackend.Models.Keys.Dto;
using NetBackend.Models.User;
using NetBackend.Services;

namespace NetBackend.Controllers;

[ApiController]
[Route("[controller]")]
[Authorize]
public class UserController : ControllerBase
{
    private readonly ILogger<UserController> _logger;
    private readonly UserManager<User> _userManager;
    private readonly IKeyService _keyService;

    public UserController(ILogger<UserController> logger, UserManager<User> userManager, IKeyService keyService)
    {
        _logger = logger;
        _userManager = userManager;
        _keyService = keyService;
    }

    [HttpGet("userinfo")]
    public async Task<ActionResult> GetUserInfo()
    {
        try
        {
            var user = await _userManager.GetUserAsync(HttpContext.User);
            if (user == null)
            {
                return Unauthorized();
            }

            return Ok(user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while fetching user info.");
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("update-database-name")]
    public async Task<IActionResult> UpdateUserDatabaseName([FromBody] UserDatabaseNameDto model)
    {
        try
        {
            var user = await _userManager.FindByEmailAsync(model.Email ?? string.Empty);
            if (user == null)
            {
                return NotFound($"User with mail {model.Email} not found.");
            }

            user.DatabaseName = model.Database switch
            {
                DatabaseType.Main => "Main",
                DatabaseType.Customer1 => "Customer1",
                DatabaseType.Customer2 => "Customer2",
                _ => user.DatabaseName // In case of an invalid enum value, do not change the database name
            };

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                return BadRequest("Failed to update user database name.");
            }

            return Ok($"User's database name updated to {user.DatabaseName}.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while updating database to user.");
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("create-accesskey")]
    public async Task<IActionResult> CreateAccessKey([FromBody] CreateAccessKeyDto model)
    {
        try
        {
            var user = await _userManager.GetUserAsync(HttpContext.User);
            if (user == null)
            {
                return Unauthorized();
            }

            // Create Api Key
            if (model.AccessibleEndpoints == null)
            {
                return BadRequest("Endpoints are be null.");
            }

            var apiKey = await _keyService.CreateApiKey(user, model.KeyName, model.AccessibleEndpoints);

            if (apiKey == null)
            {
                _logger.LogError("Failed to create API key for user: {UserId}", user.Id);
                return BadRequest("Failed to create API key.");
            }

            // Encrypt and store access key
            var accesKey = await _keyService.EncryptAndStoreAccessKey(apiKey, user);

            var accesKeyDto = new AccessKeyDto
            {
                EncryptedKey = accesKey.EncryptedKey ?? ""
            };

            return Ok(accesKeyDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while creating access key.");
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("decrypt-accesskey")]
    public async Task<IActionResult> DecryptAccessKey([FromBody] AccessKeyDto model)
    {
        try
        {
            var user = await _userManager.GetUserAsync(HttpContext.User);
            if (user == null)
            {
                return Unauthorized();
            }

            var (apiKey, errorResult) = await _keyService.DecryptAccessKeyUserCheck(model.EncryptedKey, user.Id);
            if (errorResult != null)
            {
                return errorResult;
            }

            var apiKeyDto = new ApiKeyDto
            {
                Id = apiKey?.Id ?? 0,
                KeyName = apiKey?.KeyName ?? "",
                CreatedBy = apiKey?.User.Email ?? "",
                AccessibleEndpoints = apiKey?.AccessibleEndpoints
            };

            return Ok(apiKeyDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while decrypting access key.");
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("api-endpoint-test")]
    public IActionResult GetApiEndpoint()
    {
        try
        {
            return Ok(HttpContext.Request.Path.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while fetching http context.");
            return BadRequest(ex.Message);
        }
    }
}