

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
app.MapPost("/add", (LogDbContext db, LogModel logModel) => { db.Log.Add(logModel); db.SaveChanges(); });

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