namespace CVexplorer.Models.Domain
{
    public class UserDepartmentAccess
    {
        public int Id { get; set; }


        public  required int UserId { get; set; }
        public User User { get; set; }


        public required int  DepartmentId { get; set; }
        public Department Department { get; set; }
    }
}
