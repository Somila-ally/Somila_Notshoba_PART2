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
    [Route("api/events")]
    [RequireRole]
    [SwaggerTag("Both roles can view events. Only Organisers can create events, and only the owning Organiser can update or delete them.")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public class EventsController : ControllerBase
    {
        private readonly RaceDayDbContext _context;

        public EventsController(RaceDayDbContext context)
        {
            _context = context;
        }

        /// <summary>Gets all events (both roles).</summary>
        /// <response code="200">The list of events.</response>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<EventDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetEvents()
        {
            var events = await _context.Events.Include(e => e.Organiser).OrderBy(e => e.EventDate).ToListAsync();
            return Ok(events.Select(ToDto));
        }

        /// <summary>Gets the events owned by the logged-in Organiser.</summary>
        /// <response code="200">The Organiser's own events.</response>
        /// <response code="403">Caller is not an Organiser.</response>
        [HttpGet("mine")]
        [RequireRole(RoleNames.Organiser)]
        [ProducesResponseType(typeof(IEnumerable<EventDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetMyEvents()
        {
            var organiserId = HttpContext.Session.GetInt32(SessionKeys.UserId)!.Value;
            var events = await _context.Events
                .Include(e => e.Organiser)
                .Where(e => e.OrganiserId == organiserId)
                .OrderBy(e => e.EventDate)
                .ToListAsync();
            return Ok(events.Select(ToDto));
        }

        /// <summary>Gets one event by id (both roles).</summary>
        /// <response code="200">The event.</response>
        /// <response code="404">Event not found.</response>
        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(EventDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetEvent(int id)
        {
            var ev = await _context.Events.Include(e => e.Organiser).FirstOrDefaultAsync(e => e.EventId == id);
            if (ev == null) return NotFound(new { message = "Event not found." });
            return Ok(ToDto(ev));
        }

        /// <summary>Creates an event (Organiser only).</summary>
        /// <response code="201">Event created.</response>
        /// <response code="400">Validation failed or the event type is not Run/Walk/Cycle.</response>
        /// <response code="403">Caller is not an Organiser.</response>
        [HttpPost]
        [RequireRole(RoleNames.Organiser)]
        [ProducesResponseType(typeof(EventDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> CreateEvent(CreateEventRequest request)
        {
            var eventType = EventTypes.Normalise(request.EventType);
            if (eventType == null)
                return BadRequest(new { message = $"EventType must be one of: {string.Join(", ", EventTypes.All)}." });

            var organiserId = HttpContext.Session.GetInt32(SessionKeys.UserId)!.Value;

            var ev = new Event
            {
                OrganiserId = organiserId,
                Name = request.Name.Trim(),
                Description = request.Description,
                EventDate = request.Date,
                Location = request.Location.Trim(),
                DistanceKm = request.DistanceKm,
                EventType = eventType
            };

            _context.Events.Add(ev);
            await _context.SaveChangesAsync();

            var organiser = await _context.Users.FindAsync(organiserId);
            ev.Organiser = organiser;

            return CreatedAtAction(nameof(GetEvent), new { id = ev.EventId }, ToDto(ev));
        }

        /// <summary>Updates an event (Organiser only, and only their own event).</summary>
        /// <response code="200">The updated event.</response>
        /// <response code="400">Validation failed or the event type is not Run/Walk/Cycle.</response>
        /// <response code="403">Caller is not the owning Organiser.</response>
        /// <response code="404">Event not found.</response>
        [HttpPut("{id:int}")]
        [RequireRole(RoleNames.Organiser)]
        [ProducesResponseType(typeof(EventDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateEvent(int id, UpdateEventRequest request)
        {
            var eventType = EventTypes.Normalise(request.EventType);
            if (eventType == null)
                return BadRequest(new { message = $"EventType must be one of: {string.Join(", ", EventTypes.All)}." });

            var organiserId = HttpContext.Session.GetInt32(SessionKeys.UserId)!.Value;
            var ev = await _context.Events.Include(e => e.Organiser).FirstOrDefaultAsync(e => e.EventId == id);
            if (ev == null) return NotFound(new { message = "Event not found." });
            if (ev.OrganiserId != organiserId)
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "You do not own this event." });

            ev.Name = request.Name.Trim();
            ev.Description = request.Description;
            ev.EventDate = request.Date;
            ev.Location = request.Location.Trim();
            ev.DistanceKm = request.DistanceKm;
            ev.EventType = eventType;

            await _context.SaveChangesAsync();
            return Ok(ToDto(ev));
        }

        /// <summary>Deletes an event and its categories (Organiser only, and only their own event).</summary>
        /// <response code="200">Event deleted.</response>
        /// <response code="403">Caller is not the owning Organiser.</response>
        /// <response code="404">Event not found.</response>
        /// <response code="400">The event already has enrolments, so it cannot be deleted.</response>
        [HttpDelete("{id:int}")]
        [RequireRole(RoleNames.Organiser)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> DeleteEvent(int id)
        {
            var organiserId = HttpContext.Session.GetInt32(SessionKeys.UserId)!.Value;
            var ev = await _context.Events.Include(e => e.Categories).FirstOrDefaultAsync(e => e.EventId == id);
            if (ev == null) return NotFound(new { message = "Event not found." });
            if (ev.OrganiserId != organiserId)
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "You do not own this event." });

            var hasEnrolments = await _context.Enrolments.AnyAsync(en => en.EventId == id);
            if (hasEnrolments)
                return BadRequest(new { message = "This event already has enrolments and cannot be deleted." });

            _context.Categories.RemoveRange(ev.Categories);
            _context.Events.Remove(ev);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Event deleted." });
        }

        private static EventDto ToDto(Event ev) => new()
        {
            EventId = ev.EventId,
            Name = ev.Name,
            Description = ev.Description,
            Date = ev.EventDate,
            Location = ev.Location,
            DistanceKm = ev.DistanceKm,
            EventType = ev.EventType,
            OrganiserId = ev.OrganiserId,
            OrganiserName = ev.Organiser?.FullName ?? string.Empty
        };
    }
}
