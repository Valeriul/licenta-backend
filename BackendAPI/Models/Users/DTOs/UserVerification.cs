using System.ComponentModel.DataAnnotations;

namespace BackendAPI.Models
{
    public class UserVerification
    {
        [Required]
        [StringLength(16)]
        public string Salt { get; set; }

        [Required]
        public string CentralURL { get; set; }
    }
}
