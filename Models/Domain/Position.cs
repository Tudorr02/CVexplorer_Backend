namespace CVexplorer.Models.Domain
{
    public class Position
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public int DepartmentId { get; set; }
        public required Department Department { get; set; }
    }
}
