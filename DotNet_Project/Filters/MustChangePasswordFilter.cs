using DotNet_Project.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace DotNet_Project.Filters;

public class MustChangePasswordFilter(UserManager<ApplicationUser> userManager) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var httpUser = context.HttpContext.User;
        if (httpUser.Identity?.IsAuthenticated == true)
        {
            var controller = context.RouteData.Values["controller"]?.ToString();
            var action = context.RouteData.Values["action"]?.ToString();

            var isAllowedAccountAction = string.Equals(controller, "Account", StringComparison.OrdinalIgnoreCase) &&
                (string.Equals(action, "ChangePassword", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(action, "Logout", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(action, "AccessDenied", StringComparison.OrdinalIgnoreCase));

            if (!isAllowedAccountAction)
            {
                var appUser = await userManager.GetUserAsync(httpUser);
                if (appUser?.MustChangePassword == true)
                {
                    var path = context.HttpContext.Request.Path;
                    if (path.StartsWithSegments("/api") ||
                        string.Equals(controller, "Chat", StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(action, "SendMessage", StringComparison.OrdinalIgnoreCase))
                    {
                        context.Result = new ObjectResult(new
                        {
                            success = false,
                            message = "Password change required before accessing this resource."
                        })
                        {
                            StatusCode = StatusCodes.Status403Forbidden
                        };
                        return;
                    }

                    context.Result = new RedirectToActionResult("ChangePassword", "Account", null);
                    return;
                }
            }
        }

        await next();
    }
}
