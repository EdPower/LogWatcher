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
    logDbContext.Log.RemoveRange(logDbContext.Log.Where(n => n.SentDt > deleteAfterDate));
    await logDbContext.SaveChangesAsync();
};
timer.Start();

// these are the endpoint mappings

// get all log records
app.MapGet("/all", async (LogDbContext db) => await db.Log.ToListAsync());

// get all log records since FromDt
app.MapGet("/since", async (LogDbContext db, DateTime FromDt) => await db.Log.Where(n => n.SentDt >= FromDt).ToListAsync());

// get count of all log records in database
app.MapGet("/count/all", (LogDbContext db) => db.Log.Count());

// get count of log records since FromDt
app.MapGet("/count/since", (LogDbContext db, DateTime FromDt) => db.Log.Where(n => n.SentDt >= FromDt).Count());

// add log record
app.MapPost("/add", async (LogDbContext db, LogModel logModel) =>
{
    await db.Log.AddAsync(logModel);
    await db.SaveChangesAsync();
});

// delete all log records before FromDt
// this uses ExecuteDelete for better performance on bulk deletes
app.MapDelete("/delete/before", async (LogDbContext db, DateTime FromDt) =>
{
    // need to use explicit transaction as ExecuteDeleteAsync() doesn't provide one
    // if the delete fails then transaction is rolled back when it goes out of scope
    // "await using" will call transaction.DisposeAsync() so it's disposed asynchronously
    await using var transaction = await db.Database.BeginTransactionAsync();
    await db.Log.Where(n => n.SentDt < FromDt).ExecuteDeleteAsync();
    await transaction.CommitAsync();
});

app.Run();

public class SignalRHub : Hub
{
    public async IAsyncEnumerable<DateTime> ListeningForLog([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            yield return DateTime.Now;
            await Task.Delay(1000, cancellationToken);
        }
    }
}