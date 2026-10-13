using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace RaceDay.Api.Filters
{
    /// <summary>
    /// Enforces session-based authentication and, optionally, role-based access.
    /// [RequireRole] with no arguments only requires that the user is logged in (401 otherwise).
    /// [RequireRole(RoleNames.Organiser)] additionally requires that role (403 otherwise).
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class RequireRoleAttribute : Attribute, IAuthorizationFilter
    {
        private readonly string[] _allowedRoles;

        public RequireRoleAttribute(params string[] allowedRoles)
        {
            _allowedRoles = allowedRoles;
        }

        public void OnAuthorization(AuthorizationFilterContext context)
        {
            var session = context.HttpContext.Session;
            var userId = session.GetInt32(SessionKeys.UserId);

            if (userId == null)
            {
                context.Result = new UnauthorizedObjectResult(new { message = "You must be logged in to access this resource." });
                return;
            }

            if (_allowedRoles.Length > 0)
            {
                var role = session.GetString(SessionKeys.Role);
                if (role == null || !_allowedRoles.Contains(role))
                {
                    context.Result = new ObjectResult(new { message = "You do not have permission to access this resource." })
                    {
                        StatusCode = StatusCodes.Status403Forbidden
                    };
                }
            }
        }
    }
}
