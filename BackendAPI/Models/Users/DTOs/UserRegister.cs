using System.ComponentModel.DataAnnotations;

namespace BackendAPI.Models
{
    public class UserRegister
    {
        [Required]
        [StringLength(45)]
        public string Password { get; set; }

        [Required]
        [EmailAddress]
        [StringLength(45)]
        public string Email { get; set; }

        [Required]
        [StringLength(45)]
        public string City { get; set; }

        [Required]
        [StringLength(45)]
        public string FirstName { get; set; }

        [Required]
        [StringLength(45)]
        public string LastName { get; set; }

        [Required]
        [StringLength(15)]
        public string PhoneNumber { get; set; }

        [Required]
        [StringLength(45)]
        public string Country { get; set; }

        [Required]
        [StringLength(45)]
        public string Gender { get; set; }
    }
}
