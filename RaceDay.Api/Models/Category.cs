using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.Models
{
    /// <summary>A category defined by an Organiser for one event (e.g. "Under 20", "Senior", "10km Run").</summary>
    public class Category
    {
        public int CategoryId { get; set; }

        public int EventId { get; set; }
        public Event? Event { get; set; }

        [Required, MaxLength(100)]
        public string CategoryName { get; set; } = string.Empty;

        public decimal DistanceKm { get; set; }

        public int MaxParticipants { get; set; } = 100;

        public decimal EntryFee { get; set; } = 0;

        public ICollection<Enrolment> Enrolments { get; set; } = new List<Enrolment>();
    }
}
