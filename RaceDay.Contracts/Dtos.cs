using System.ComponentModel.DataAnnotations;

namespace RaceDay.Contracts
{
    // =====================================================================
    //  Shared request/response objects used by the API (and, in Part 3, the
    //  MVC application). A DTO is not a database table - models represent
    //  stored entities, DTOs represent data moving into or out of the API.
    // =====================================================================

    // ----------------------------- Authentication -----------------------------

    /// <summary>Payload for registering a new Organiser or Participant account.</summary>
    public class RegisterRequest
    {
        [Required, MaxLength(45)]
        public string FirstName { get; set; } = string.Empty;

        [Required, MaxLength(45)]
        public string LastName { get; set; } = string.Empty;

        [Required, EmailAddress, MaxLength(150)]
        public string Email { get; set; } = string.Empty;

        /// <summary>Minimum 6 characters. Stored only as a salted hash.</summary>
        [Required, MinLength(6), MaxLength(100)]
        public string Password { get; set; } = string.Empty;

        /// <summary>Must be exactly "Organiser" or "Participant".</summary>
        [Required]
        public string Role { get; set; } = string.Empty;
    }

    /// <summary>Payload for logging in.</summary>
    public class LoginRequest
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;
    }

    /// <summary>Returned after a successful registration or login.</summary>
    public class AuthResponse
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    // -------------------------------- Profile ---------------------------------

    /// <summary>The logged-in user's own profile.</summary>
    public class ProfileDto
    {
        public int UserId { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
    }

    /// <summary>Payload for updating the logged-in user's own profile.</summary>
    public class UpdateProfileRequest
    {
        [Required, MaxLength(45)]
        public string FirstName { get; set; } = string.Empty;

        [Required, MaxLength(45)]
        public string LastName { get; set; } = string.Empty;
    }

    // --------------------------------- Events ---------------------------------

    /// <summary>Payload for creating an event.</summary>
    public class CreateEventRequest
    {
        [Required, MaxLength(150)]
        public string Name { get; set; } = string.Empty;

        [Required, MaxLength(500)]
        public string Description { get; set; } = string.Empty;

        [Required]
        public DateTime Date { get; set; }

        [Required, MaxLength(150)]
        public string Location { get; set; } = string.Empty;

        /// <summary>Overall event distance in kilometres (0.01 - 999.99).</summary>
        [Range(0.01, 999.99)]
        public decimal DistanceKm { get; set; }

        /// <summary>"Run", "Walk" or "Cycle".</summary>
        [Required]
        public string EventType { get; set; } = string.Empty;
    }

    /// <summary>Payload for updating an event (same shape as creating one).</summary>
    public class UpdateEventRequest : CreateEventRequest
    {
    }

    /// <summary>An event as returned to clients.</summary>
    public class EventDto
    {
        public int EventId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime Date { get; set; }
        public string Location { get; set; } = string.Empty;
        public decimal DistanceKm { get; set; }
        public string EventType { get; set; } = string.Empty;
        public int OrganiserId { get; set; }
        public string OrganiserName { get; set; } = string.Empty;
    }

    // ------------------------------- Categories -------------------------------

    /// <summary>Payload for creating or updating a category (e.g. "Under 20", "Senior", "10km").</summary>
    public class CreateCategoryRequest
    {
        [Required, MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        /// <summary>Optional. Defaults to the parent event's distance (age-group categories have no distance of their own).</summary>
        [Range(0.01, 999.99)]
        public decimal? DistanceKm { get; set; }

        [Range(1, 100000)]
        public int MaxParticipants { get; set; } = 100;

        [Range(0, 999999.99)]
        public decimal EntryFee { get; set; } = 0;
    }

    /// <summary>A category as returned to clients.</summary>
    public class CategoryDto
    {
        public int CategoryId { get; set; }
        public int EventId { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal DistanceKm { get; set; }
        public int MaxParticipants { get; set; }
        public decimal EntryFee { get; set; }
    }

    // ------------------------------- Enrolments -------------------------------

    /// <summary>Payload for a Participant entering an event under a chosen category.</summary>
    public class CreateEnrolmentRequest
    {
        [Range(1, int.MaxValue)]
        public int EventId { get; set; }

        [Range(1, int.MaxValue)]
        public int CategoryId { get; set; }
    }

    /// <summary>Payload for an Organiser changing an enrolment's status.</summary>
    public class UpdateEnrolmentStatusRequest
    {
        /// <summary>"Pending", "Confirmed" or "Cancelled".</summary>
        [Required]
        public string Status { get; set; } = string.Empty;
    }

    /// <summary>An enrolment as returned to clients.</summary>
    public class EnrolmentDto
    {
        public int EnrolmentId { get; set; }
        public int EventId { get; set; }
        public string EventName { get; set; } = string.Empty;
        public int ParticipantId { get; set; }
        public string ParticipantName { get; set; } = string.Empty;
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public DateTime EnrolmentDate { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    // --------------------------------- Results --------------------------------

    /// <summary>Payload for an Organiser recording the result of one enrolment.</summary>
    public class CreateResultRequest
    {
        [Range(1, int.MaxValue)]
        public int EnrolmentId { get; set; }

        /// <summary>Finish time in the format hh:mm:ss (e.g. "00:47:32").</summary>
        [Required]
        public string FinishTime { get; set; } = string.Empty;

        [Range(1, int.MaxValue)]
        public int FinishPosition { get; set; }
    }

    /// <summary>A result as returned to clients.</summary>
    public class ResultDto
    {
        public int ResultId { get; set; }
        public int EnrolmentId { get; set; }
        public int EventId { get; set; }
        public string EventName { get; set; } = string.Empty;
        public string ParticipantName { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public string FinishTime { get; set; } = string.Empty;
        public int? FinishPosition { get; set; }
        public DateTime RecordedAt { get; set; }
    }

    // ---------------------------- Part 3 (image upload) ----------------------------

    /// <summary>Returned after an image upload (used by the Part 3 MVC application).</summary>
    public class UploadResponse
    {
        public string FileName { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
    }
}
