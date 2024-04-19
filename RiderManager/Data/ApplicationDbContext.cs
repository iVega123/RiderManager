using Microsoft.EntityFrameworkCore;
using RiderManager.Models;

namespace RiderManager.Data
{
    public class ApplicationDbContext : DbContext, IApplicationDbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
            Database.EnsureCreated();
        }

        public DbSet<Rider> Riders { get; set; }

        public DbSet<PresignedUrl> PresignedUrls { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Rider>()
                .HasIndex(m => m.CNPJ)
                .IsUnique();

            modelBuilder.Entity<Rider>()
                .HasIndex(u => u.CNHNumber)
                .IsUnique();

            modelBuilder.Entity<Rider>()
                .HasOne(r => r.CNHUrl)
                .WithOne(p => p.Rider)
                .HasForeignKey<PresignedUrl>(p => p.RiderId);

            modelBuilder.Entity<PresignedUrl>()
                .HasIndex(p => p.ObjectName)
                .IsUnique();
        }
    }
}
