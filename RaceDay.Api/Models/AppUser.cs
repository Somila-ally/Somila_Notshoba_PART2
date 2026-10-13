using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.Models
{
    /// <summary>
    /// An Organiser or Participant account, distinguished by RoleId (the ERD's Roles table).
    /// Mapped to the "Users" table. Named AppUser to avoid clashing with ControllerBase.User.
    /// </summary>
    public class AppUser
    {
        public int UserId { get; set; }

        [Required, MaxLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Required, MaxLength(150)]
        public string Email { get; set; } = string.Empty;

        /// <summary>Salted hash produced by ASP.NET Core's PasswordHasher. The plain-text password is never stored.</summary>
        [Required, MaxLength(255)]
        public string PasswordHash { get; set; } = string.Empty;

        public int RoleId { get; set; }
        public Role? Role { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<Event> OrganisedEvents { get; set; } = new List<Event>();
        public ICollection<Enrolment> Enrolments { get; set; } = new List<Enrolment>();
    }
}
