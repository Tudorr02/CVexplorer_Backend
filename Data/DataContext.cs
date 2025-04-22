using CVexplorer.Models.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using CVexplorer.Models.Primitives;
using System.Linq.Expressions;
using static System.Runtime.InteropServices.JavaScript.JSType;
using CVexplorer.Enums;
using System.Text.Json.Serialization;
using System;

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

        public DbSet<CvEvaluationResult> CvEvaluationResults { get; set; }  // ← nou

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

            // CV -> CvEvaluationResult
            modelBuilder.Entity<CV>()
                .HasOne(c => c.Evaluation)
                .WithOne(e => e.Cv)
                .HasForeignKey<CvEvaluationResult>(e => e.CvId)
                .OnDelete(DeleteBehavior.Cascade);

            var jsonOptions = new JsonSerializerOptions();
            var enumJsonOptions = new JsonSerializerOptions
            {
                Converters =
                {
                    new JsonStringEnumConverter()
                }
            };


            // 1) A generic JSON converter +comparer for any T
            ValueConverter<T, string> JsonConverter<T> () => new(
                v => JsonSerializer.Serialize(v, jsonOptions),
                v => JsonSerializer.Deserialize<T>(v, jsonOptions)!);

            

            ValueComparer<T> JsonComparer<T>() => new(
                (a, b) => JsonSerializer.Serialize(a, jsonOptions) == JsonSerializer.Serialize(b, jsonOptions),
                a => JsonSerializer.Serialize(a, jsonOptions).GetHashCode(),
                v => JsonSerializer.Deserialize<T>(
                            JsonSerializer.Serialize(v, jsonOptions), jsonOptions)!);


            // For Level and MinimumEducationLevel, use enumJsonOptions
            ValueConverter<T, string> JsonEnumConverter<T>() => new(
               v => JsonSerializer.Serialize(v, enumJsonOptions),
               v => JsonSerializer.Deserialize<T>(v, enumJsonOptions)!);



            ValueComparer<T> JsonEnumComparer<T>() => new(
                (a, b) => JsonSerializer.Serialize(a, enumJsonOptions) == JsonSerializer.Serialize(b, enumJsonOptions),
                a => JsonSerializer.Serialize(a, enumJsonOptions).GetHashCode(),
                v => JsonSerializer.Deserialize<T>(
                            JsonSerializer.Serialize(v, enumJsonOptions), enumJsonOptions)!);


            // 2) Map each field as its own JSON column:
            modelBuilder.Entity<CvEvaluationResult>(e =>
            {
                

                // CandidateName + EvaluatedAt as before
                e.Property(x => x.CandidateName)
                 .IsRequired()
                 .HasMaxLength(200);
                

                // ── now the JSON columns ──

                // CvScoreScrapedField<List<string>> types:
                e.Property(x => x.RequiredSkills)
                 .HasColumnType("nvarchar(max)")
                 .HasConversion(JsonConverter<CvScoreScrapedField<List<string>>>())
                 .Metadata.SetValueComparer(JsonComparer<CvScoreScrapedField<List<string>>>());

                e.Property(x => x.NiceToHave)
                 .HasColumnType("nvarchar(max)")
                 .HasConversion(JsonConverter<CvScoreScrapedField<List<string>>>())
                 .Metadata.SetValueComparer(JsonComparer<CvScoreScrapedField<List<string>>>());

                e.Property(x => x.Certifications)
                 .HasColumnType("nvarchar(max)")
                 .HasConversion(JsonConverter<CvScoreScrapedField<List<string>>>())
                 .Metadata.SetValueComparer(JsonComparer<CvScoreScrapedField<List<string>>>());

                e.Property(x => x.Responsibilities)
                 .HasColumnType("nvarchar(max)")
                 .HasConversion(JsonConverter<CvScoreScrapedField<List<string>>>())
                 .Metadata.SetValueComparer(JsonComparer<CvScoreScrapedField<List<string>>>());

                // CvScoreValueField<List<string>>:
                e.Property(x => x.Languages)
                 .HasColumnType("nvarchar(max)")
                 .HasConversion(JsonConverter<CvScoreValueField<List<string>>>())
                 .Metadata.SetValueComparer(JsonComparer<CvScoreValueField<List<string>>>());

                // CvScoreValueField<double>:
                e.Property(x => x.MinimumExperienceMonths)
                 .HasColumnType("nvarchar(max)")
                 .HasConversion(JsonConverter<CvScoreValueField<double>>())
                 .Metadata.SetValueComparer(JsonComparer<CvScoreValueField<double>>());

                // CvScoreValueField<PositionLevel>:
                e.Property(x => x.Level)
                 .HasColumnType("nvarchar(max)")
                 .HasConversion(JsonEnumConverter<CvScoreValueField<PositionLevel>>())
                 .Metadata.SetValueComparer(JsonEnumComparer <CvScoreValueField<PositionLevel>>());

                // CvScoreValueField<EducationLevel>:
                e.Property(x => x.MinimumEducationLevel)
                 .HasColumnType("nvarchar(max)")
                 .HasConversion(JsonEnumConverter<CvScoreValueField<EducationLevel>>())
                 .Metadata.SetValueComparer(JsonEnumComparer < CvScoreValueField<EducationLevel>> ());
            });


            modelBuilder.Entity<Position>()
              .HasMany(p => p.Rounds)
              .WithOne(r => r.Position)
              .HasForeignKey(r => r.PositionId)
              .OnDelete(DeleteBehavior.Cascade);


            modelBuilder.Entity<Round>()
                .HasMany(r => r.RoundEntries)
                .WithOne(e => e.Round)
                .HasForeignKey(e => e.RoundId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CV>()
              .HasMany(cv => cv.RoundEntries)
              .WithOne(e => e.Cv)
              .HasForeignKey(e => e.CvId)
              .OnDelete(DeleteBehavior.ClientCascade);
        }


    }

    
}
