using System.Globalization;
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
    [Route("api/results")]
    [RequireRole]
    [SwaggerTag("Organisers record each enrolled Participant's finish time and position for events they own. Participants can only view their own results.")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public class ResultsController : ControllerBase
    {
        private readonly RaceDayDbContext _context;

        public ResultsController(RaceDayDbContext context)
        {
            _context = context;
        }

        /// <summary>Records the finish time and position for one enrolment (owning Organiser only).</summary>
        /// <response code="201">Result recorded.</response>
        /// <response code="400">Validation failed, invalid FinishTime, the enrolment is cancelled, or a result is already recorded.</response>
        /// <response code="403">Caller does not own the enrolment's event.</response>
        /// <response code="404">Enrolment not found.</response>
        [HttpPost]
        [RequireRole(RoleNames.Organiser)]
        [ProducesResponseType(typeof(ResultDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> CreateResult(CreateResultRequest request)
        {
            var organiserId = HttpContext.Session.GetInt32(SessionKeys.UserId)!.Value;

            var enrolment = await _context.Enrolments
                .Include(en => en.Event)
                .Include(en => en.Category)
                .Include(en => en.Participant)
                .FirstOrDefaultAsync(en => en.EnrolmentId == request.EnrolmentId);
            if (enrolment == null) return NotFound(new { message = "Enrolment not found." });

            // The Organiser must own the event the enrolment belongs to.
            if (enrolment.Event!.OrganiserId != organiserId)
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "You do not own this event." });

            if (enrolment.Status == EnrolmentStatuses.Cancelled)
                return BadRequest(new { message = "A result cannot be recorded for a cancelled enrolment." });

            if (!TimeSpan.TryParse(request.FinishTime, CultureInfo.InvariantCulture, out var finishTime) || finishTime <= TimeSpan.Zero)
                return BadRequest(new { message = "FinishTime must be a positive time in the format hh:mm:ss." });

            var alreadyRecorded = await _context.Results.AnyAsync(r => r.EnrolmentId == enrolment.EnrolmentId);
            if (alreadyRecorded)
                return BadRequest(new { message = "A result has already been recorded for this enrolment." });

            var result = new Result
            {
                EnrolmentId = enrolment.EnrolmentId,
                FinishTime = finishTime,
                Position = request.FinishPosition
            };

            _context.Results.Add(result);
            await _context.SaveChangesAsync();

            result.Enrolment = enrolment;
            return StatusCode(StatusCodes.Status201Created, ToDto(result));
        }

        /// <summary>Gets the logged-in Participant's own results.</summary>
        /// <response code="200">The Participant's results.</response>
        /// <response code="403">Caller is not a Participant.</response>
        [HttpGet("mine")]
        [RequireRole(RoleNames.Participant)]
        [ProducesResponseType(typeof(IEnumerable<ResultDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetMyResults()
        {
            var participantId = HttpContext.Session.GetInt32(SessionKeys.UserId)!.Value;

            var results = await _context.Results
                .Include(r => r.Enrolment).ThenInclude(en => en!.Event)
                .Include(r => r.Enrolment).ThenInclude(en => en!.Category)
                .Include(r => r.Enrolment).ThenInclude(en => en!.Participant)
                .Where(r => r.Enrolment!.ParticipantId == participantId)
                .ToListAsync();

            return Ok(results.Select(ToDto));
        }

        /// <summary>Gets all results for an event (owning Organiser only).</summary>
        /// <response code="200">The event's results, ordered by position.</response>
        /// <response code="403">Caller does not own the event.</response>
        /// <response code="404">Event not found.</response>
        [HttpGet("event/{eventId:int}")]
        [RequireRole(RoleNames.Organiser)]
        [ProducesResponseType(typeof(IEnumerable<ResultDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetEventResults(int eventId)
        {
            var organiserId = HttpContext.Session.GetInt32(SessionKeys.UserId)!.Value;
            var ev = await _context.Events.FindAsync(eventId);
            if (ev == null) return NotFound(new { message = "Event not found." });
            if (ev.OrganiserId != organiserId)
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "You do not own this event." });

            var results = await _context.Results
                .Include(r => r.Enrolment).ThenInclude(en => en!.Event)
                .Include(r => r.Enrolment).ThenInclude(en => en!.Category)
                .Include(r => r.Enrolment).ThenInclude(en => en!.Participant)
                .Where(r => r.Enrolment!.EventId == eventId)
                .OrderBy(r => r.Position)
                .ToListAsync();

            return Ok(results.Select(ToDto));
        }

        private static ResultDto ToDto(Result r) => new()
        {
            ResultId = r.ResultId,
            EnrolmentId = r.EnrolmentId,
            EventId = r.Enrolment?.EventId ?? 0,
            EventName = r.Enrolment?.Event?.Name ?? string.Empty,
            ParticipantName = r.Enrolment?.Participant?.FullName ?? string.Empty,
            CategoryName = r.Enrolment?.Category?.CategoryName ?? string.Empty,
            FinishTime = r.FinishTime.ToString("c", CultureInfo.InvariantCulture),
            FinishPosition = r.Position,
            RecordedAt = r.RecordedAt
        };
    }
}
