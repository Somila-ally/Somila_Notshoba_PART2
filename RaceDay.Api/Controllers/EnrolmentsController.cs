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
    [Route("api/enrolments")]
    [RequireRole]
    [SwaggerTag("A Participant enters an event by choosing the event and one of its categories. Organisers can view and manage enrolments for their own events only.")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public class EnrolmentsController : ControllerBase
    {
        private readonly RaceDayDbContext _context;

        public EnrolmentsController(RaceDayDbContext context)
        {
            _context = context;
        }

        /// <summary>Enters the logged-in Participant into an event under a chosen category.</summary>
        /// <response code="201">Enrolment created.</response>
        /// <response code="400">Validation failed, the category does not belong to the event, you are already enrolled in it, or it is full.</response>
        /// <response code="403">Caller is not a Participant.</response>
        /// <response code="404">Event not found.</response>
        [HttpPost]
        [RequireRole(RoleNames.Participant)]
        [ProducesResponseType(typeof(EnrolmentDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Enrol(CreateEnrolmentRequest request)
        {
            var participantId = HttpContext.Session.GetInt32(SessionKeys.UserId)!.Value;

            var eventExists = await _context.Events.AnyAsync(e => e.EventId == request.EventId);
            if (!eventExists) return NotFound(new { message = "Event not found." });

            var category = await _context.Categories
                .FirstOrDefaultAsync(c => c.CategoryId == request.CategoryId && c.EventId == request.EventId);
            if (category == null)
                return BadRequest(new { message = "This category does not belong to the specified event." });

            var alreadyEnrolled = await _context.Enrolments
                .AnyAsync(en => en.CategoryId == request.CategoryId && en.ParticipantId == participantId);
            if (alreadyEnrolled)
                return BadRequest(new { message = "You are already enrolled in this category." });

            var taken = await _context.Enrolments
                .CountAsync(en => en.CategoryId == category.CategoryId && en.Status != EnrolmentStatuses.Cancelled);
            if (taken >= category.MaxParticipants)
                return BadRequest(new { message = "This category is full." });

            var enrolment = new Enrolment
            {
                ParticipantId = participantId,
                EventId = request.EventId,
                CategoryId = request.CategoryId
            };

            _context.Enrolments.Add(enrolment);
            await _context.SaveChangesAsync();

            var created = await _context.Enrolments
                .Include(en => en.Event)
                .Include(en => en.Category)
                .Include(en => en.Participant)
                .FirstAsync(en => en.EnrolmentId == enrolment.EnrolmentId);

            return StatusCode(StatusCodes.Status201Created, ToDto(created));
        }

        /// <summary>Gets the logged-in Participant's own enrolments.</summary>
        /// <response code="200">The Participant's enrolments.</response>
        /// <response code="403">Caller is not a Participant.</response>
        [HttpGet("mine")]
        [RequireRole(RoleNames.Participant)]
        [ProducesResponseType(typeof(IEnumerable<EnrolmentDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetMyEnrolments()
        {
            var participantId = HttpContext.Session.GetInt32(SessionKeys.UserId)!.Value;

            var enrolments = await _context.Enrolments
                .Include(en => en.Event)
                .Include(en => en.Category)
                .Include(en => en.Participant)
                .Where(en => en.ParticipantId == participantId)
                .ToListAsync();

            return Ok(enrolments.Select(ToDto));
        }

        /// <summary>Gets all enrolments for an event (owning Organiser only).</summary>
        /// <response code="200">The event's enrolments.</response>
        /// <response code="403">Caller does not own the event.</response>
        /// <response code="404">Event not found.</response>
        [HttpGet("event/{eventId:int}")]
        [RequireRole(RoleNames.Organiser)]
        [ProducesResponseType(typeof(IEnumerable<EnrolmentDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetEventEnrolments(int eventId)
        {
            var organiserId = HttpContext.Session.GetInt32(SessionKeys.UserId)!.Value;
            var ev = await _context.Events.FindAsync(eventId);
            if (ev == null) return NotFound(new { message = "Event not found." });
            if (ev.OrganiserId != organiserId)
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "You do not own this event." });

            var enrolments = await _context.Enrolments
                .Include(en => en.Event)
                .Include(en => en.Category)
                .Include(en => en.Participant)
                .Where(en => en.EventId == eventId)
                .ToListAsync();

            return Ok(enrolments.Select(ToDto));
        }

        /// <summary>Changes an enrolment's status to Pending, Confirmed or Cancelled (owning Organiser only).</summary>
        /// <response code="200">The updated enrolment.</response>
        /// <response code="400">Invalid status.</response>
        /// <response code="403">Caller does not own the enrolment's event.</response>
        /// <response code="404">Enrolment not found.</response>
        [HttpPut("{id:int}/status")]
        [RequireRole(RoleNames.Organiser)]
        [ProducesResponseType(typeof(EnrolmentDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateStatus(int id, UpdateEnrolmentStatusRequest request)
        {
            var status = EnrolmentStatuses.Normalise(request.Status);
            if (status == null)
                return BadRequest(new { message = $"Status must be one of: {string.Join(", ", EnrolmentStatuses.All)}." });

            var organiserId = HttpContext.Session.GetInt32(SessionKeys.UserId)!.Value;
            var enrolment = await _context.Enrolments
                .Include(en => en.Event)
                .Include(en => en.Category)
                .Include(en => en.Participant)
                .FirstOrDefaultAsync(en => en.EnrolmentId == id);
            if (enrolment == null) return NotFound(new { message = "Enrolment not found." });
            if (enrolment.Event!.OrganiserId != organiserId)
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "You do not own this event." });

            enrolment.Status = status;
            await _context.SaveChangesAsync();

            return Ok(ToDto(enrolment));
        }

        private static EnrolmentDto ToDto(Enrolment en) => new()
        {
            EnrolmentId = en.EnrolmentId,
            EventId = en.EventId,
            EventName = en.Event?.Name ?? string.Empty,
            ParticipantId = en.ParticipantId,
            ParticipantName = en.Participant?.FullName ?? string.Empty,
            CategoryId = en.CategoryId,
            CategoryName = en.Category?.CategoryName ?? string.Empty,
            EnrolmentDate = en.EnrolmentDate,
            Status = en.Status
        };
    }
}
