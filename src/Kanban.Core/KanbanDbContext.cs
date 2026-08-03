using Kanban.Core.Entities;
using Kanban.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace Kanban.Core;

public class KanbanDbContext : DbContext
{
    public KanbanDbContext(DbContextOptions<KanbanDbContext> options) : base(options) { }

    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Card> Cards => Set<Card>();
    public DbSet<CardNote> CardNotes => Set<CardNote>();
    public DbSet<Run> Runs => Set<Run>();
    public DbSet<RunLogLine> RunLogLines => Set<RunLogLine>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Project>(e =>
        {
            e.Property(p => p.Name).HasMaxLength(100).IsRequired();
            e.Property(p => p.Path).HasMaxLength(500).IsRequired();
            e.HasIndex(p => p.Name).IsUnique();
        });

        b.Entity<Card>(e =>
        {
            e.Property(c => c.Title).HasMaxLength(200).IsRequired();
            e.Property(c => c.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
            e.Property(c => c.Outcome).HasConversion<string>().HasMaxLength(20);
            e.Property(c => c.BranchName).HasMaxLength(200);
            e.HasIndex(c => new { c.Status, c.Position });
            e.HasOne(c => c.Project)
             .WithMany(p => p.Cards)
             .HasForeignKey(c => c.ProjectId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<CardNote>(e =>
        {
            e.Property(n => n.Author).HasConversion<string>().HasMaxLength(10).IsRequired();
            e.Property(n => n.Body).IsRequired();
            e.HasOne(n => n.Card)
             .WithMany(c => c.Notes)
             .HasForeignKey(n => n.CardId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Run>(e =>
        {
            e.Property(r => r.Outcome).HasConversion<string>().HasMaxLength(20);
            e.Property(r => r.FailureReason).HasMaxLength(500);
            e.Property(r => r.BranchName).HasMaxLength(200).IsRequired();
            e.Property(r => r.BaseCommitSha).HasMaxLength(40).IsRequired();
            e.HasOne(r => r.Card)
             .WithMany(c => c.Runs)
             .HasForeignKey(r => r.CardId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<RunLogLine>(e =>
        {
            e.Property(l => l.Stream).HasConversion<string>().HasMaxLength(6).IsRequired();
            e.Property(l => l.Text).IsRequired();
            e.HasIndex(l => new { l.RunId, l.Seq });
            e.HasOne(l => l.Run)
             .WithMany(r => r.LogLines)
             .HasForeignKey(l => l.RunId)
             .OnDelete(DeleteBehavior.Cascade);
        });
    }
}