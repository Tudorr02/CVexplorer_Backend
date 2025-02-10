namespace CVexplorer.Models.Domain
{
    public class Department
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public int CompanyId { get; set; }
        public required Company Company { get; set; }
        
        public ICollection<Position> Positions { get; set; } = [];
        public ICollection<UserDepartmentAccess> UserDepartmentAccesses { get; set; } = [];
    }
}
