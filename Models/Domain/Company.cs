namespace CVexplorer.Models.Domain
{
    public class Company
    {
        public int Id { get; set; }
        public  required string Name { get; set; }
       
        public ICollection<Department> Departments { get; set; } = [];

        public ICollection<User> Users { get; set; } = [];
    }
}
