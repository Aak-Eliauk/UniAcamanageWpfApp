using Microsoft.EntityFrameworkCore;
using UniAcamanageWpfApp.Models;

namespace UniAcamanageWpfApp.Data
{
    public class CampusDbContext : DbContext
    {
        // 使用新的类名
        public DbSet<ClassroomSpatial> ClassroomSpatials { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlServer(
                @"Server=LAPTOP-9ALD8MJ8\SQLSERVER;Database=UniAcademicDB;Trusted_Connection=True;TrustServerCertificate=True",
                x => x.UseNetTopologySuite()
            );
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // 确保实体映射到正确的表
            modelBuilder.Entity<ClassroomSpatial>()
                .ToTable("Classroom");
        }
    }
}