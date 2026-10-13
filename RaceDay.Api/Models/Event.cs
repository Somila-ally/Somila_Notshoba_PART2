using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RaceDay.Api.Models
{
    /// <summary>A race day event created by an Organiser.</summary>
    public class Event
    {
        public int EventId { get; set; }

        public int OrganiserId { get; set; }
        public AppUser? Organiser { get; set; }

        /// <summary>The event's name (Name in the Part 2 brief); stored in the ERD column "EventName".</summary>
        [Required, MaxLength(150), Column("EventName")]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        public DateTime EventDate { get; set; }

        [Required, MaxLength(150)]
        public string Location { get; set; } = string.Empty;

        /// <summary>Overall distance of the event (required by the Part 2 brief; see README "Differences from Part 1").</summary>
        public decimal DistanceKm { get; set; }

        /// <summary>Run, Walk or Cycle (required by the Part 2 brief; see README "Differences from Part 1").</summary>
        [Required, MaxLength(20)]
        public string EventType { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<Category> Categories { get; set; } = new List<Category>();
        public ICollection<Enrolment> Enrolments { get; set; } = new List<Enrolment>();
    }
}
