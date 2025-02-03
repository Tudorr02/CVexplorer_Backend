using System.ComponentModel.DataAnnotations;

namespace CVexplorer.Models.DTO
{
    public class RegisterDTO
    {
        [MaxLength(60)]
        public required string Username { get; set; }
        public required string Password { get; set; }   
    }
}
