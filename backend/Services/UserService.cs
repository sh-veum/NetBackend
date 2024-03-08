using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using NetBackend.Models.User;
using NetBackend.Services.Interfaces;

namespace NetBackend.Services;

public class UserService : IUserService
{
    private readonly UserManager<UserModel> _userManager;
    private readonly ILogger<UserService> _logger;

    public UserService(UserManager<UserModel> userManager, ILogger<UserService> logger)
    {
        _userManager = userManager;
        _logger = logger;
    }

    public async Task<(UserModel user, ActionResult error)> GetUserAsync(HttpContext httpContext)
    {
        _logger.LogInformation("Getting user from HttpContext");
        var user = await _userManager.GetUserAsync(httpContext.User);
        if (user == null)
        {
            _logger.LogWarning("User not found in HttpContext");
            var httpContextUser = httpContext.User.Identity?.Name;
            _logger.LogWarning("HttpContext.User.Identity.Name: {Name}", httpContextUser);

            return (null!, new UnauthorizedObjectResult(new
            {
                message = $"User not found. {httpContextUser}"
            }));
        }

        _logger.LogInformation("User found: {Email}", user.Email);
        return (user, null!);
    }
}