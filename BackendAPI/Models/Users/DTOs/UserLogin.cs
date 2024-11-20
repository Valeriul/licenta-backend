using System.ComponentModel.DataAnnotations;

namespace BackendAPI.Models
{
    public class UserLogin
    {
        
        [Required]
        [StringLength(45)]
        public string Email { get; set; }

        [Required]
        [StringLength(45)]
        public string Password { get; set; }
    }

    public class UserLoginWithSalt
    {
        [Required]
        [StringLength(16)]
        public string Salt { get; set; }

    }
}
