namespace RaceDay.Api.Models
{
    /// <summary>The finish time and position captured by an Organiser for one enrolment (one-to-one).</summary>
    public class Result
    {
        public int ResultId { get; set; }

        public int EnrolmentId { get; set; }
        public Enrolment? Enrolment { get; set; }

        public TimeSpan FinishTime { get; set; }

        /// <summary>Nullable in the ERD.</summary>
        public int? Position { get; set; }

        public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
    }
}
