using LogWatcher.Models;
using System.Runtime.CompilerServices;
using System.Threading;

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

// get the max age for log records from appsettings.json, defaulting to 30 days
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

// get all log records since fromDt at logLevel or higher for a customer
app.MapGet("/since", async (LogDbContext db, DateTime fromDt, int logLevel, string customerId) =>
{
    return string.IsNullOrEmpty(customerId) || customerId.Equals("All")
    ? await db.Log.Where(n => (n.SentDt >= fromDt) && ((int)n.Level >= logLevel)).ToListAsync()
    : await db.Log.Where(n => (n.SentDt >= fromDt) && ((int)n.Level >= logLevel) && (n.CustomerId != null) && n.CustomerId.Equals(customerId)).ToListAsync();
});

// get list of customers
app.MapGet("/customers", async (LogDbContext db) => await db.Log.Select(n => n.CustomerId).Distinct().ToListAsync());

// add log record
app.MapPost("/add", async (LogDbContext db, LogModel logModel) =>
{
    await db.Log.AddAsync(logModel);
    await db.SaveChangesAsync();
});

app.Run();

// this is used to echo recently added records 
public class SignalRHub : Hub
{
    static CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
    static CancellationToken cancellationToken;

    // this is called by client to cancel updates
    public void StopLogUpdates()
    {
        cancellationTokenSource.Cancel();
    }

    // this is called by client to start receiving log updates
    public async IAsyncEnumerable<LogModel> GetLogUpdates(string customer, int levelFilter)
    {
        cancellationTokenSource = new CancellationTokenSource();
        cancellationToken = cancellationTokenSource.Token;
        var lastChecked = DateTime.Now;

        using var logDbContext = new LogDbContext();
        while (!cancellationToken.IsCancellationRequested)
        {
            foreach (var logModel in logDbContext.Log
                .Where(n => customer.Equals("All") || ((n.CustomerId != null) && n.CustomerId.Equals(customer)))
                .Where(n => ((int)n.Level & levelFilter) > 0)
                .Where(n => n.SentDt > lastChecked))
            {
                yield return logModel;
            }
            lastChecked = DateTime.Now;
            await Task.Delay(1000, cancellationToken);
        }
    }
}
