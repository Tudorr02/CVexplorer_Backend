namespace CVexplorer.Models.DTO
{
    public class CompanyManagementListDTO
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public int? Employees { get; set; }
    }
}
