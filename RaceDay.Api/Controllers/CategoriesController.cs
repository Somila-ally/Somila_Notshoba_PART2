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
    [Route("api")]
    [RequireRole]
    [SwaggerTag("Categories (age groups or distances) for an event. Both roles can view them; only the Organiser who owns the event can create, update or delete them.")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public class CategoriesController : ControllerBase
    {
        private readonly RaceDayDbContext _context;

        public CategoriesController(RaceDayDbContext context)
        {
            _context = context;
        }

        /// <summary>Gets all categories of an event (both roles).</summary>
        /// <response code="200">The categories.</response>
        /// <response code="404">Event not found.</response>
        [HttpGet("events/{eventId:int}/categories")]
        [ProducesResponseType(typeof(IEnumerable<CategoryDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetCategories(int eventId)
        {
            var eventExists = await _context.Events.AnyAsync(e => e.EventId == eventId);
            if (!eventExists) return NotFound(new { message = "Event not found." });

            var categories = await _context.Categories.Where(c => c.EventId == eventId).ToListAsync();
            return Ok(categories.Select(ToDto));
        }

        /// <summary>Adds a category to an event (owning Organiser only). DistanceKm defaults to the event's distance.</summary>
        /// <response code="201">Category created.</response>
        /// <response code="400">Validation failed.</response>
        /// <response code="403">Caller does not own the event.</response>
        /// <response code="404">Event not found.</response>
        [HttpPost("events/{eventId:int}/categories")]
        [RequireRole(RoleNames.Organiser)]
        [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> CreateCategory(int eventId, CreateCategoryRequest request)
        {
            var organiserId = HttpContext.Session.GetInt32(SessionKeys.UserId)!.Value;
            var ev = await _context.Events.FindAsync(eventId);
            if (ev == null) return NotFound(new { message = "Event not found." });
            if (ev.OrganiserId != organiserId)
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "You do not own this event." });

            var category = new Category
            {
                EventId = eventId,
                CategoryName = request.Name.Trim(),
                DistanceKm = request.DistanceKm ?? ev.DistanceKm,
                MaxParticipants = request.MaxParticipants,
                EntryFee = request.EntryFee
            };

            _context.Categories.Add(category);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetCategories), new { eventId }, ToDto(category));
        }

        /// <summary>Updates a category (owning Organiser only). Omitting DistanceKm keeps the current distance.</summary>
        /// <response code="200">The updated category.</response>
        /// <response code="400">Validation failed.</response>
        /// <response code="403">Caller does not own the parent event.</response>
        /// <response code="404">Category not found.</response>
        [HttpPut("categories/{id:int}")]
        [RequireRole(RoleNames.Organiser)]
        [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateCategory(int id, CreateCategoryRequest request)
        {
            var organiserId = HttpContext.Session.GetInt32(SessionKeys.UserId)!.Value;
            var category = await _context.Categories.Include(c => c.Event).FirstOrDefaultAsync(c => c.CategoryId == id);
            if (category == null) return NotFound(new { message = "Category not found." });
            if (category.Event!.OrganiserId != organiserId)
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "You do not own this event." });

            category.CategoryName = request.Name.Trim();
            category.DistanceKm = request.DistanceKm ?? category.DistanceKm;
            category.MaxParticipants = request.MaxParticipants;
            category.EntryFee = request.EntryFee;

            await _context.SaveChangesAsync();
            return Ok(ToDto(category));
        }

        /// <summary>Deletes a category (owning Organiser only).</summary>
        /// <response code="200">Category deleted.</response>
        /// <response code="403">Caller does not own the parent event.</response>
        /// <response code="404">Category not found.</response>
        /// <response code="400">The category already has enrolments, so it cannot be deleted.</response>
        [HttpDelete("categories/{id:int}")]
        [RequireRole(RoleNames.Organiser)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> DeleteCategory(int id)
        {
            var organiserId = HttpContext.Session.GetInt32(SessionKeys.UserId)!.Value;
            var category = await _context.Categories.Include(c => c.Event).FirstOrDefaultAsync(c => c.CategoryId == id);
            if (category == null) return NotFound(new { message = "Category not found." });
            if (category.Event!.OrganiserId != organiserId)
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "You do not own this event." });

            var hasEnrolments = await _context.Enrolments.AnyAsync(en => en.CategoryId == id);
            if (hasEnrolments)
                return BadRequest(new { message = "This category already has enrolments and cannot be deleted." });

            _context.Categories.Remove(category);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Category deleted." });
        }

        private static CategoryDto ToDto(Category c) => new()
        {
            CategoryId = c.CategoryId,
            EventId = c.EventId,
            Name = c.CategoryName,
            DistanceKm = c.DistanceKm,
            MaxParticipants = c.MaxParticipants,
            EntryFee = c.EntryFee
        };
    }
}
