using Microsoft.EntityFrameworkCore;
using UniAcamanageWpfApp.Models;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NetTopologySuite.Geometries;

namespace UniAcamanageWpfApp.Data
{
    public class CampusDbContext : DbContext
    {
        public DbSet<ClassroomSpatial> ClassroomSpatials { get; set; }
        public DbSet<Course> Courses { get; set; }
        public DbSet<StudentCourse> StudentCourses { get; set; }
        public DbSet<TeacherCourse> TeacherCourses { get; set; }
        public DbSet<Semester> Semesters { get; set; }
        public DbSet<Student> Students { get; set; }
        public DbSet<Teacher> Teachers { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlServer(
                @"Server=LAPTOP-9ALD8MJ8\SQLSERVER;Database=UniAcademicDB;Trusted_Connection=True;TrustServerCertificate=True",
                x => x.UseNetTopologySuite()
            );
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // 配置 ClassroomSpatial
            modelBuilder.Entity<ClassroomSpatial>(entity =>
            {
                entity.ToTable("Classroom");
                entity.HasKey(e => e.ClassroomID);

                entity.Property(e => e.RoomNumber)
                    .HasMaxLength(20)
                    .IsRequired();

                entity.Property(e => e.SpatialLocation)
                    .HasMaxLength(100);

                entity.Property(e => e.Shape)
                    .HasColumnType("geometry");

                entity.Property(e => e.CenterPoint)
                    .HasColumnType("geometry");
            });

            // 配置 Course
            modelBuilder.Entity<Course>(entity =>
            {
                entity.HasKey(e => e.CourseID);
                entity.Property(e => e.CourseCode).HasMaxLength(20);
                entity.Property(e => e.CourseName).HasMaxLength(200);
                entity.Property(e => e.CourseType).HasMaxLength(50);
                entity.Property(e => e.ScheduleTime).HasMaxLength(100);
                entity.Property(e => e.Description).HasMaxLength(500);
            });

            // 配置 StudentCourse
            modelBuilder.Entity<StudentCourse>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.StudentID).HasMaxLength(20);
                entity.Property(e => e.SelectionType).HasMaxLength(20);
                entity.Property(e => e.RejectReason).HasMaxLength(255);
            });

            // 配置 TeacherCourse
            modelBuilder.Entity<TeacherCourse>(entity =>
            {
                entity.HasKey(tc => new { tc.TeacherID, tc.CourseID });
                entity.Property(e => e.TeacherID).HasMaxLength(20);
            });
        }
    }
}