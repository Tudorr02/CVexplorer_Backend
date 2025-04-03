using CVexplorer.Models.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace CVexplorer.Data
{
    public class DataContext(DbContextOptions options) : IdentityDbContext<User,Role,int,IdentityUserClaim<int>, 
        UserRole,IdentityUserLogin<int>, IdentityRoleClaim<int>, IdentityUserToken<int>>(options)
    {

        /// Users DbSet already exists in IdentityDbContext
        /// Roles DbSet already exists in IdentityDbContext

        public DbSet<Company> Companies { get; set; }
        public DbSet<Department> Departments { get; set; }
        public DbSet<UserDepartmentAccess> UserDepartmentAccesses { get; set; }
        public DbSet<Position> Positions { get; set; }
        public DbSet<CV> CVs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Company -> Users
            modelBuilder.Entity<User>()
                .HasOne(u => u.Company)
                .WithMany(c => c.Users)
                .HasForeignKey(u => u.CompanyId)
                .OnDelete(DeleteBehavior.Cascade);

            // Company -> Departments
            modelBuilder.Entity<Department>()
                .HasOne(d => d.Company)
                .WithMany(c => c.Departments)
                .HasForeignKey(d => d.CompanyId)
                .OnDelete(DeleteBehavior.Cascade);

            // Department -> Positions
            modelBuilder.Entity<Position>()
                .HasOne(p => p.Department)
                .WithMany(d => d.Positions)
                .HasForeignKey(p => p.DepartmentId)
                .OnDelete(DeleteBehavior.Cascade);

            // Department -> UserDepartmentAccess
            modelBuilder.Entity<UserDepartmentAccess>()
                .HasOne(uda => uda.Department)
                .WithMany(d => d.UserDepartmentAccesses)
                .HasForeignKey(uda => uda.DepartmentId)
                .OnDelete(DeleteBehavior.ClientCascade); // 👈 simulate cascade

            // User -> UserDepartmentAccess
            modelBuilder.Entity<UserDepartmentAccess>()
                .HasOne(uda => uda.User)
                .WithMany(u => u.UserDepartmentAccesses)
                .HasForeignKey(uda => uda.UserId)
                .OnDelete(DeleteBehavior.Cascade); 



            modelBuilder.Entity<UserRole>()
               .HasIndex(ur => ur.UserId)
               .IsUnique();

            modelBuilder.Entity<UserRole>()
                .HasOne(ur => ur.User)
                .WithMany(u => u.UserRoles)
                .HasForeignKey(ur => ur.UserId);

            modelBuilder.Entity<UserRole>()
                .HasOne(ur => ur.Role)
                .WithMany(r => r.UserRoles)
                .HasForeignKey(ur => ur.RoleId);


            var stringListConverter = new ValueConverter<List<string>, string>(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions)null) ?? new List<string>());

            var stringListComparer = new ValueComparer<List<string>>(
                (c1, c2) => c1.SequenceEqual(c2),
                c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                c => c.ToList());

            modelBuilder.Entity<Position>()
                 .Property(p => p.RequiredSkills)
                 .HasConversion(stringListConverter)
                 .Metadata.SetValueComparer(stringListComparer);

            modelBuilder.Entity<Position>()
                .Property(p => p.NiceToHave)
                .HasConversion(stringListConverter)
                .Metadata.SetValueComparer(stringListComparer);

            modelBuilder.Entity<Position>()
                .Property(p => p.Languages)
                .HasConversion(stringListConverter)
                .Metadata.SetValueComparer(stringListComparer);

            modelBuilder.Entity<Position>()
                .Property(p => p.Certification)
                .HasConversion(stringListConverter)
                .Metadata.SetValueComparer(stringListComparer);

            modelBuilder.Entity<Position>()
                .Property(p => p.Responsibilities)
                .HasConversion(stringListConverter)
                .Metadata.SetValueComparer(stringListComparer);

            // Enums as string (optional but readable)
            modelBuilder.Entity<Position>()
                .Property(p => p.Level)
                .HasConversion<string>();

            modelBuilder.Entity<Position>()
                .Property(p => p.MinimumEducationLevel)
                .HasConversion<string>();

            // CV -> Position
            modelBuilder.Entity<CV>()
                .HasOne(cv => cv.Position)
                .WithMany()
                .HasForeignKey(cv => cv.PositionId)
                .OnDelete(DeleteBehavior.Cascade);

            // CV -> User
            modelBuilder.Entity<CV>()
                .HasOne(cv => cv.UserUploadedBy)
                .WithMany()
                .HasForeignKey(cv => cv.UserUploadedById)
                .OnDelete(DeleteBehavior.ClientSetNull);

        }


    }

    
}
