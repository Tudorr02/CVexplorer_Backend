using CVexplorer.Models.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>()
                .HasMany(u => u.UserRoles)
                .WithOne(ur => ur.User)
                .HasForeignKey(ur => ur.UserId)
                .IsRequired();

            modelBuilder.Entity<Role>()
                .HasMany(r => r.UserRoles)
                .WithOne(ur => ur.Role)
                .HasForeignKey(ur => ur.RoleId)
                .IsRequired();

            modelBuilder.Entity<Department>()
                 .HasOne(d => d.Company)
                 .WithMany(c => c.Departments)
                 .HasForeignKey(d => d.CompanyId)
                 .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Position>()
                .HasOne(p => p.Department)
                .WithMany(d => d.Positions)
                .HasForeignKey(p => p.DepartmentId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<UserDepartmentAccess>()
              .HasOne(uda => uda.User)
              .WithMany(u => u.UserDepartmentAccesses)
              .HasForeignKey(uda => uda.UserId)
              .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<UserDepartmentAccess>()
                .HasOne(uda => uda.Department)
                .WithMany(d => d.UserDepartmentAccesses)
                .HasForeignKey(uda => uda.DepartmentId)
                .OnDelete(DeleteBehavior.Cascade);
        }

    }
}
