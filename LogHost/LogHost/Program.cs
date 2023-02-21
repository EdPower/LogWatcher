using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.EntityFrameworkCore.Storage;
using System.Reflection.Metadata;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("log") ?? "Data source=log.db";
builder.Services.AddSqlite<LogDbContext>(connectionString);
builder.Services.AddSignalR();

using (var logDbContext = new LogDbContext())
{
    logDbContext.Database.EnsureCreated();
}

var app = builder.Build();

app.MapHub<SignalRHub>("/ReceiveLog");

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.MapGet("/", async (LogDbContext db) => await db.Log.ToListAsync());
app.MapGet("/since", async (LogDbContext db, DateTime FromDt) => await db.Log.Where(n => n.SentDt >= FromDt).ToListAsync());
app.MapPost("/add", (LogDbContext db, LogModel logModel) => { db.Log.Add(logModel); db.SaveChanges(); return Results.Ok; });

app.Run();

class LogDbContext : DbContext
{
    public LogDbContext() { }
    public LogDbContext(DbContextOptions options) : base(options) { }

    public DbSet<LogModel> Log { get; set; } = null!; // initialize with null-forgiving operator to prevent compiler warning 

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite("Filename=log.db");
        base.OnConfiguring(optionsBuilder);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Map table names
        modelBuilder.Entity<LogModel>().ToTable("Log");
        modelBuilder.Entity<LogModel>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Id).IsUnique();
            entity.HasIndex(e => e.SentDt);
        });
        base.OnModelCreating(modelBuilder);
    }
}

public class SignalRHub: Hub
{
    public async IAsyncEnumerable<DateTime>ListeningForLog(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            yield return DateTime.Now;
            await Task.Delay(1000, cancellationToken);
        }
    }
}