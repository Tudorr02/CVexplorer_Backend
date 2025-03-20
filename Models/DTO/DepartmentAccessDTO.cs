namespace CVexplorer.Models.DTO
{
    public class DepartmentAccessDTO
    {
        public int UserId { get; set; }
        public string UserName { get; set; }

        public required bool HasAccess { get; set; } = false;
    }
}
