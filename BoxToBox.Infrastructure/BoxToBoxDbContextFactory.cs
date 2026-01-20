using BoxToBox.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using System;

namespace BoxToBox.Infrastructure
{
    // Provides a DbContext for design-time tools (dotnet ef) so migrations work even when the app runs with InMemory in DEBUG.
    public class BoxToBoxDbContextFactory : IDesignTimeDbContextFactory<BoxToBoxDbContext>
    {
        public BoxToBoxDbContext CreateDbContext(string[] args)
        {
            // Prefer a configured SQLite connection string, fallback to a local file
            var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Sqlite")
                                    ?? "Data Source=boxtobox-migrations.db";

            var optionsBuilder = new DbContextOptionsBuilder<BoxToBoxDbContext>();
            optionsBuilder.UseSqlite(connectionString);

            return new BoxToBoxDbContext(optionsBuilder.Options);
        }
    }
}
