namespace CVexplorer.Models.DTO
{
    public class CompanyUserDTO
    {
        public string Username { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Email { get; set; }
        public List<string> UserRoles { get; set; } = [];
    }
}
