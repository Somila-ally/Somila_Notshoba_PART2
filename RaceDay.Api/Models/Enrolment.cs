using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.Models
{
    /// <summary>
    /// Records a Participant entering an Event under a chosen Category. The unique index on
    /// (ParticipantId, CategoryId) means a participant may enter several categories of one
    /// event (e.g. 5km and 10km) but never the same category twice.
    /// </summary>
    public class Enrolment
    {
        public int EnrolmentId { get; set; }

        public int ParticipantId { get; set; }
        public AppUser? Participant { get; set; }

        public int EventId { get; set; }
        public Event? Event { get; set; }

        public int CategoryId { get; set; }
        public Category? Category { get; set; }

        public DateTime EnrolmentDate { get; set; } = DateTime.UtcNow;

        [Required, MaxLength(20)]
        public string Status { get; set; } = EnrolmentStatuses.Pending;

        public Result? Result { get; set; }
    }
}
