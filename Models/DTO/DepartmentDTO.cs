namespace CVexplorer.Models.DTO
{
    public class DepartmentDTO
    {
        public required string Name { get; set; }
        public List<DepartmentAccessDTO> ? DepartmentAccesses { get; set; } = [];  
    }
}
