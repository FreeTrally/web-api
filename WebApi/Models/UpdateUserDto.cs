using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace WebApi.Models
{
    public class UpdateUserDto
    {
        public Guid Id { get; set; }
        [Required]
        [RegularExpression("^[0-9\\p{L}]*$", ErrorMessage = "Login should contain only letters or digits")]
        public string Login { get; set; }
        public string LastName { get; set; }
        public string FirstName { get; set; }
    }
}