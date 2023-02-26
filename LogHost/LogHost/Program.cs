

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

// get all log records
app.MapGet("/", async (LogDbContext db) => await db.Log.ToListAsync());

// get all log records since FromDt
app.MapGet("/since", async (LogDbContext db, DateTime FromDt) => await db.Log.Where(n => n.SentDt >= FromDt).ToListAsync());

// add log record
app.MapPost("/add", async (LogDbContext db, LogModel logModel) =>
{
    await db.Log.AddAsync(logModel);
    await db.SaveChangesAsync();
});

// delete all log records since FromDt
app.MapDelete("/delete/since", async (LogDbContext db, DateTime FromDt) =>
{
    await  db.Log.Where(n => n.SentDt >= FromDt).ExecuteDeleteAsync();
});


// delete first count records
app.MapDelete("/delete/first", async (LogDbContext db, int count) =>
{
    await db.Log.Take(count).ExecuteDeleteAsync();
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