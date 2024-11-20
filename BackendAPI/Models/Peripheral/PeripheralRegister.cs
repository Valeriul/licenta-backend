using System.ComponentModel.DataAnnotations;

namespace BackendAPI.Models
{
    public class PeripheralRegister
    {
        [Required]
        [StringLength(45)]
        public string uuid { get; set; }

        [Required]
        [StringLength(45)]
        public string type { get; set; }

    }
}