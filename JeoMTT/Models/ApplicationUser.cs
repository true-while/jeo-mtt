using Microsoft.AspNetCore.Identity;

namespace JeoMTT.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string Nickname { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
