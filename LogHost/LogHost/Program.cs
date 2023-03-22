using LogWatcher.Models;
using System.Runtime.CompilerServices;

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

// get the max age for log records from appsettings.json
int deleteAfterDays = int.TryParse(builder.Configuration["DeleteAfterDays"], out deleteAfterDays) ? deleteAfterDays : 30;
var deleteAfterDate = DateTime.Now.AddDays(-deleteAfterDays);

// setup a timer to delete old records, checking every hour
var timer = new System.Timers.Timer(TimeSpan.FromHours(1));
timer.Elapsed += async (sender, e) =>
{
    using var logDbContext = new LogDbContext();
    logDbContext.Log.RemoveRange(logDbContext.Log.Where(n => n.SentDt < deleteAfterDate));
    await logDbContext.SaveChangesAsync();
};
timer.Start();


// these are the endpoint mappings

// get all log records
app.MapGet("/all", async (LogDbContext db) => await db.Log.ToListAsync());

// get all log records since fromDt at logLevel or higher for a customer
app.MapGet("/since", async (LogDbContext db, DateTime fromDt, int logLevel, string customerId) =>
{
    return string.IsNullOrEmpty(customerId) || customerId.Equals("All")
    ? await db.Log.Where(n => (n.SentDt >= fromDt) && ((int)n.Level >= logLevel)).ToListAsync()
    : await db.Log.Where(n => (n.SentDt >= fromDt) && ((int)n.Level >= logLevel) && (n.CustomerId != null) && n.CustomerId.Equals(customerId)).ToListAsync();
});

// get count of all log records in database for a customer
app.MapGet("/count", (LogDbContext db, string customerId) =>
{
    return string.IsNullOrEmpty(customerId) || customerId.Equals("All")
        ? db.Log.Count()
        : db.Log.Where(n => n.CustomerId != null && n.CustomerId.Equals(customerId)).Count();
});

// get list of customers
app.MapGet("/customers", async (LogDbContext db) => await db.Log.Select(n => n.CustomerId).Distinct().ToListAsync());

// add log record
app.MapPost("/add", async (LogDbContext db, LogModel logModel) =>
{
    await db.Log.AddAsync(logModel);
    await db.SaveChangesAsync();
});

// delete all log records before fromDt
// this uses ExecuteDelete for better performance on bulk deletes
app.MapDelete("/delete/before", async (LogDbContext db, DateTime fromDt) =>
{
    // need to use explicit transaction as ExecuteDeleteAsync() doesn't provide one
    // if the delete fails then transaction is rolled back when it goes out of scope
    // "await using" will call transaction.DisposeAsync() so it's disposed asynchronously
    await using var transaction = await db.Database.BeginTransactionAsync();
    await db.Log.Where(n => n.SentDt < fromDt).ExecuteDeleteAsync();
    await transaction.CommitAsync();
});

// delete all log records (just used for testing)
app.MapDelete("/delete/all", async (LogDbContext db) =>
{
    await using var transaction = await db.Database.BeginTransactionAsync();
    await db.Log.ExecuteDeleteAsync();
    await transaction.CommitAsync();
});

app.Run();

public class SignalRHub : Hub
{
    private DateTime lastChecked = DateTime.Now;

    public async IAsyncEnumerable<LogModel> LogUpdates(string customer, int minLevel, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var logDbContext = new LogDbContext();
        while (!cancellationToken.IsCancellationRequested)
        {
            foreach (var logModel in logDbContext.Log.Where(n => (n.CustomerId != null) && n.CustomerId.Equals(customer) && ((int)n.Level >= minLevel) && (n.SentDt > lastChecked)))
            {
                yield return logModel;
            }
            lastChecked = DateTime.Now;
            await Task.Delay(1000, cancellationToken);
        }
    }
}