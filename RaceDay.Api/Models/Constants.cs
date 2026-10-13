namespace RaceDay.Api.Models
{
    /// <summary>
    /// The two role names seeded into the Roles table (RoleId 1 = Organiser, 2 = Participant).
    /// Named RoleNames (not Roles) so it can never clash with the DbSet&lt;Role&gt; Roles property.
    /// </summary>
    public static class RoleNames
    {
        public const string Organiser = "Organiser";
        public const string Participant = "Participant";
    }

    /// <summary>The kinds of event an Organiser can create.</summary>
    public static class EventTypes
    {
        public const string Run = "Run";
        public const string Walk = "Walk";
        public const string Cycle = "Cycle";

        public static readonly string[] All = { Run, Walk, Cycle };

        /// <summary>Returns the canonical spelling of a valid event type (case-insensitive), or null if invalid.</summary>
        public static string? Normalise(string? value) =>
            All.FirstOrDefault(t => string.Equals(t, value?.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>The statuses an enrolment can have (Part 2 brief). New enrolments start as Pending.</summary>
    public static class EnrolmentStatuses
    {
        public const string Pending = "Pending";
        public const string Confirmed = "Confirmed";
        public const string Cancelled = "Cancelled";

        public static readonly string[] All = { Pending, Confirmed, Cancelled };

        /// <summary>Returns the canonical spelling of a valid status (case-insensitive), or null if invalid.</summary>
        public static string? Normalise(string? value) =>
            All.FirstOrDefault(s => string.Equals(s, value?.Trim(), StringComparison.OrdinalIgnoreCase));
    }
}
