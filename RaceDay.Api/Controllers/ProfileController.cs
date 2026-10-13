using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Filters;
using RaceDay.Api.Models;
using RaceDay.Contracts;
using Swashbuckle.AspNetCore.Annotations;

namespace RaceDay.Api.Controllers
{
    [ApiController]
    [Route("api/profile")]
    [RequireRole]
    [SwaggerTag("Both roles can view and update their own profile. The user is always taken from the session - never from the URL.")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public class ProfileController : ControllerBase
    {
        private readonly RaceDayDbContext _context;

        public ProfileController(RaceDayDbContext context)
        {
            _context = context;
        }

        /// <summary>Gets the profile of the logged-in user.</summary>
        /// <response code="200">The profile.</response>
        /// <response code="401">Not logged in.</response>
        [HttpGet("me")]
        [ProducesResponseType(typeof(ProfileDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetMyProfile()
        {
            var userId = HttpContext.Session.GetInt32(SessionKeys.UserId)!.Value;
            var user = await _context.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.UserId == userId);
            if (user == null) return Unauthorized(new { message = "Your account no longer exists." });

            return Ok(ToDto(user));
        }

        /// <summary>Updates the first and last name of the logged-in user.</summary>
        /// <response code="200">The updated profile.</response>
        /// <response code="400">Validation failed.</response>
        /// <response code="401">Not logged in.</response>
        [HttpPut("me")]
        [ProducesResponseType(typeof(ProfileDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UpdateMyProfile(UpdateProfileRequest request)
        {
            var userId = HttpContext.Session.GetInt32(SessionKeys.UserId)!.Value;
            var user = await _context.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.UserId == userId);
            if (user == null) return Unauthorized(new { message = "Your account no longer exists." });

            user.FullName = $"{request.FirstName.Trim()} {request.LastName.Trim()}";
            await _context.SaveChangesAsync();

            return Ok(ToDto(user));
        }

        private static ProfileDto ToDto(AppUser user)
        {
            var parts = user.FullName.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return new ProfileDto
            {
                UserId = user.UserId,
                FirstName = parts.Length > 0 ? parts[0] : string.Empty,
                LastName = parts.Length > 1 ? parts[1] : string.Empty,
                FullName = user.FullName,
                Email = user.Email,
                Role = user.Role?.RoleName ?? string.Empty
            };
        }
    }
}
