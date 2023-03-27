// LogHost


var builder = WebApplication.CreateBuilder(args);
 
// get connection string from appsettings.json or use default
var connectionString = builder.Configuration.GetConnectionString("log") ?? "Data source=log.db";

// get the max age for log records from appsettings.json, defaulting to 30 days
int deleteAfterDays = int.TryParse(builder.Configuration["DeleteAfterDays"], out deleteAfterDays) ? deleteAfterDays : 30;
var deleteAfterDate = DateTime.Now.AddDays(-deleteAfterDays);

// use sqlite database
builder.Services.AddSqlite<LogDbContext>(connectionString);

builder.Services.AddSignalR();

// create database if it doesn't exist
using (var logDbContext = new LogDbContext())
{
    logDbContext.Database.EnsureCreated();
}

var app = builder.Build();

// set SignalR Hub 
app.MapHub<SignalRHub>("/ReceiveLog");

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

// map endpoints

// get all log records since fromDt at logLevel or higher for a customer
app.MapGet("/since", async (LogDbContext db, DateTime fromDt, int logLevel, string customerId) =>
{
    return await db.Log
             .AsNoTracking()
             .Where(n => n.SentDt >= fromDt)
             .Where(n => customerId.Equals("All") || (n.CustomerId != null && n.CustomerId.Equals(customerId)))
             .Where(n => (int)n.Level >= logLevel)
             .ToListAsync();
});

// get list of customers
app.MapGet("/customers", async (LogDbContext db) =>
    await db.Log.Select(n => n.CustomerId).Distinct().ToListAsync());

// add log record
app.MapPost("/add", async (LogDbContext db, LogModel logModel) =>
{
    await db.Log.AddAsync(logModel);
    await db.SaveChangesAsync();
});


// setup a timer to delete old records once an hour
var timer = new System.Timers.Timer(TimeSpan.FromHours(1));
timer.Elapsed += async (sender, e) =>
{
    using var logDbContext = new LogDbContext();
    logDbContext.Log.RemoveRange(logDbContext.Log.Where(n => n.SentDt < deleteAfterDate));
    await logDbContext.SaveChangesAsync();
};
timer.Start();



app.Run();
