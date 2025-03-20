namespace CVexplorer.Models.DTO
{
    public class DepartmentTreeNodeDTO
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public List<PositionTreeNodeDTO> Positions { get; set; } = [];
    }
}
