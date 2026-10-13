using Microsoft.AspNetCore.Identity;
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
    [Route("api/auth")]
    [SwaggerTag("Registration, login and logout. A successful login starts a server-side session that stores the UserId and Role.")]
    public class AuthController : ControllerBase
    {
        private readonly RaceDayDbContext _context;
        private readonly IPasswordHasher<AppUser> _hasher;

        public AuthController(RaceDayDbContext context, IPasswordHasher<AppUser> hasher)
        {
            _context = context;
            _hasher = hasher;
        }

        /// <summary>Registers a new Organiser or Participant. The password is hashed before it is saved.</summary>
        /// <response code="201">Account created.</response>
        /// <response code="400">Validation failed, the role is not "Organiser"/"Participant", or the email is already registered.</response>
        [HttpPost("register")]
        [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Register(RegisterRequest request)
        {
            if (request.Role != RoleNames.Organiser && request.Role != RoleNames.Participant)
                return BadRequest(new { message = "Role must be either 'Organiser' or 'Participant'." });

            var role = await _context.Roles.FirstOrDefaultAsync(r => r.RoleName == request.Role);
            if (role == null)
                return BadRequest(new { message = "Role must be either 'Organiser' or 'Participant'." });

            var email = request.Email.Trim();
            if (await _context.Users.AnyAsync(u => u.Email == email))
                return BadRequest(new { message = "An account with this email already exists." });

            var user = new AppUser
            {
                FullName = $"{request.FirstName.Trim()} {request.LastName.Trim()}",
                Email = email,
                RoleId = role.RoleId
            };
            user.PasswordHash = _hasher.HashPassword(user, request.Password);

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return StatusCode(StatusCodes.Status201Created, new AuthResponse
            {
                UserId = user.UserId,
                FullName = user.FullName,
                Email = user.Email,
                Role = role.RoleName,
                Message = "Registration successful."
            });
        }

        /// <summary>Logs in with email and password, then stores the UserId and Role in the session.</summary>
        /// <response code="200">Login successful; the session cookie is issued.</response>
        /// <response code="401">Invalid email or password.</response>
        [HttpPost("login")]
        [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Login(LoginRequest request)
        {
            var email = request.Email.Trim();
            var user = await _context.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Email == email);
            if (user == null)
                return Unauthorized(new { message = "Invalid email or password." });

            var verification = _hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
            if (verification == PasswordVerificationResult.Failed)
                return Unauthorized(new { message = "Invalid email or password." });

            var roleName = user.Role!.RoleName;

            HttpContext.Session.Clear();
            HttpContext.Session.SetInt32(SessionKeys.UserId, user.UserId);
            HttpContext.Session.SetString(SessionKeys.Role, roleName);
            HttpContext.Session.SetString(SessionKeys.Email, user.Email);

            return Ok(new AuthResponse
            {
                UserId = user.UserId,
                FullName = user.FullName,
                Email = user.Email,
                Role = roleName,
                Message = "Login successful."
            });
        }

        /// <summary>Logs out by clearing the session. Protected endpoints return 401 afterwards.</summary>
        /// <response code="200">Logged out.</response>
        /// <response code="401">Not logged in.</response>
        [HttpPost("logout")]
        [RequireRole]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return Ok(new { message = "Logout successful." });
        }
    }
}
