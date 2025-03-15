namespace CVexplorer.Models.DTO
{
    public class DepartmentDTO
    {
        public required string Name { get; set; }
        public List<string> Positions { get; set; } = []; // ✅ Stores position names 
    }
}
