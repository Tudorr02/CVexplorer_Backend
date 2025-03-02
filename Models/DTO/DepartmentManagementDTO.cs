namespace CVexplorer.Models.DTO
{
    public class DepartmentManagementDTO
    {
        public required string Name { get; set; }
        public string CompanyName { get; set; } = string.Empty; // ✅ Stores company name
        public List<string> Positions { get; set; } = []; // ✅ Stores position names 
    }
}
