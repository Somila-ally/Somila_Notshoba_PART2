using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.Models
{
    /// <summary>A system role (Organiser or Participant). Matches the ERD's Roles lookup table.</summary>
    public class Role
    {
        public int RoleId { get; set; }

        [Required, MaxLength(20)]
        public string RoleName { get; set; } = string.Empty;

        public ICollection<AppUser> Users { get; set; } = new List<AppUser>();
    }
}
