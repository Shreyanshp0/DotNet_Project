using DotNet_Project.Data;
using DotNet_Project.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace DotNet_Project.Tests;

public static class TestDbContextFactory
{
    public static (ApplicationDbContext Db, SqliteConnection Connection) CreateInMemoryDbContext()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        var db = new ApplicationDbContext(options);
        db.Database.EnsureCreated();

        return (db, connection);
    }

    public static async Task<(ApplicationDbContext Db, SqliteConnection Connection, ApplicationUser Parent, ApplicationUser Child, ChildProfile Profile)> CreateSeededTestContextAsync()
    {
        var (db, conn) = CreateInMemoryDbContext();
        var id = Guid.NewGuid().ToString("N");
        var parent = new ApplicationUser
        {
            Id = $"parent-{id}",
            UserName = $"parent_{id}@test.com",
            Email = $"parent_{id}@test.com",
            FullName = "Jane Smith"
        };
        var child = new ApplicationUser
        {
            Id = $"child-{id}",
            UserName = $"child_{id}@test.com",
            Email = $"child_{id}@test.com",
            FullName = "Alex Smith"
        };
        db.Users.AddRange(parent, child);
        await db.SaveChangesAsync();

        var profile = new ChildProfile
        {
            ChildUserId = child.Id,
            ParentUserId = parent.Id,
            ChildName = child.FullName,
            DateOfBirth = new DateOnly(2014, 3, 10),
            GradeOrAgeGroup = "Grade 7"
        };
        db.ChildProfiles.Add(profile);
        await db.SaveChangesAsync();

        return (db, conn, parent, child, profile);
    }
}
