using DotNet_Project.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace DotNet_Project.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<ChildProfile> ChildProfiles => Set<ChildProfile>();
    public DbSet<MoodEntry> MoodEntries => Set<MoodEntry>();
    public DbSet<Assessment> Assessments => Set<Assessment>();
    public DbSet<AssessmentAnswer> AssessmentAnswers => Set<AssessmentAnswer>();
    public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();
    public DbSet<AIReport> AIReports => Set<AIReport>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>().Property(x => x.FullName).HasMaxLength(200).IsRequired();
        builder.Entity<ChildProfile>(e =>
        {
            e.HasIndex(x => x.ChildUserId).IsUnique();
            e.HasIndex(x => x.ParentUserId);
            e.Property(x => x.ChildName).HasMaxLength(200).IsRequired();
            e.Property(x => x.Gender).HasMaxLength(50);
            e.Property(x => x.GradeOrAgeGroup).HasMaxLength(100);
            e.HasOne(x => x.ParentUser).WithMany(x => x.ParentChildProfiles).HasForeignKey(x => x.ParentUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.ChildUser).WithOne(x => x.ChildProfile).HasForeignKey<ChildProfile>(x => x.ChildUserId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<MoodEntry>(e =>
        {
            e.Property(x => x.Mood).HasMaxLength(80).IsRequired();
            e.HasOne(x => x.ChildProfile).WithMany(x => x.MoodEntries).HasForeignKey(x => x.ChildProfileId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.ChildProfileId, x.CreatedAt });
        });
        builder.Entity<Assessment>(e =>
        {
            e.Property(x => x.Status).HasMaxLength(40).IsRequired();
            e.HasOne(x => x.ChildProfile).WithMany(x => x.Assessments).HasForeignKey(x => x.ChildProfileId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.ChildProfileId, x.AssessmentDate });
        });
        builder.Entity<AssessmentAnswer>(e =>
        {
            e.ToTable(t => t.HasCheckConstraint("CK_AssessmentAnswers_QuestionId", "QuestionId BETWEEN 1 AND 10"));
            e.Property(x => x.QuestionText).HasMaxLength(1000).IsRequired();
            e.Property(x => x.Answer).HasMaxLength(500).IsRequired();
            e.HasIndex(x => new { x.AssessmentId, x.QuestionId }).IsUnique();
            e.HasOne(x => x.Assessment).WithMany(x => x.Answers).HasForeignKey(x => x.AssessmentId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<JournalEntry>(e =>
        {
            e.Property(x => x.BestPart).HasMaxLength(4000).IsRequired();
            e.Property(x => x.DifficultPart).HasMaxLength(4000).IsRequired();
            e.Property(x => x.DayDescription).HasMaxLength(4000).IsRequired();
            e.Property(x => x.HelpRequired).HasMaxLength(2000);
            e.HasIndex(x => new { x.ChildProfileId, x.EntryDate }).IsUnique();
            e.HasOne(x => x.ChildProfile).WithMany(x => x.JournalEntries).HasForeignKey(x => x.ChildProfileId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<AIReport>(e =>
        {
            e.Property(x => x.AssessmentId).IsRequired();
            e.HasIndex(x => x.AssessmentId).IsUnique();
            e.HasOne(x => x.Assessment).WithOne(x => x.AIReport).HasForeignKey<AIReport>(x => x.AssessmentId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<ChatMessage>(e =>
        {
            e.Property(x => x.ConversationId).HasMaxLength(100).IsRequired();
            e.Property(x => x.Sender).HasMaxLength(40).IsRequired();
            e.Property(x => x.Message).HasMaxLength(4000).IsRequired();
            e.HasIndex(x => new { x.ChildProfileId, x.ConversationId, x.CreatedAt });
            e.HasOne(x => x.ChildProfile).WithMany(x => x.ChatMessages).HasForeignKey(x => x.ChildProfileId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
