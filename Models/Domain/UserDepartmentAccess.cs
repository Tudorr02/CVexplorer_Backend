namespace CVexplorer.Models.Domain
{
    public class UserDepartmentAccess
    {
        public int Id { get; set; }


        public int UserId { get; set; }
        public required User User { get; set; }


        public int DepartmentId { get; set; }
        public required Department Department { get; set; }
    }
}
