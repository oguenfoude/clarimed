using FocusMed.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace FocusMed.Data;

public class FocusMedDbContext : DbContext
{
    public FocusMedDbContext(DbContextOptions<FocusMedDbContext> options) : base(options)
    {
    }

    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<Study> Studies => Set<Study>();
    public DbSet<Series> Series => Set<Series>();
    public DbSet<DicomImage> Images => Set<DicomImage>();

    public DbSet<Document> Documents => Set<Document>();
    public DbSet<ClinicSettings> ClinicSettings => Set<ClinicSettings>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Patient>(entity =>
        {
            entity.HasIndex(e => e.PatientId);
            entity.HasIndex(e => e.Name);
        });

        modelBuilder.Entity<Study>(entity =>
        {
            entity.HasIndex(e => e.StudyInstanceUid).IsUnique();
            entity.HasIndex(e => e.StudyDate);
            entity.HasIndex(e => e.Modality);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.IsDeleted);
            entity.HasIndex(e => e.AccessionNumber);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.DeletedAt);
            
            entity.HasQueryFilter(s => !s.IsDeleted);
        });

        modelBuilder.Entity<Series>(entity =>
        {
            entity.HasIndex(e => e.SeriesInstanceUid).IsUnique();
        });

        modelBuilder.Entity<DicomImage>(entity =>
        {
            entity.HasIndex(e => e.SopInstanceUid).IsUnique();
        });

        modelBuilder.Entity<Document>(entity =>
        {
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.ReceivedAt);
            entity.HasIndex(e => e.StudyId);
        });



        modelBuilder.Entity<ClinicSettings>(entity =>
        {
            entity.HasKey(e => e.Id);
        });
    }
}
