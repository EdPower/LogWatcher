namespace LogHost
{
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

        // this is called by client to start receiving log updates, filtered by customer and loglevel
        public async IAsyncEnumerable<LogModel> GetLogUpdatesAsync(string customer, int levelFilter)
        {
            cancellationTokenSource = new CancellationTokenSource();
            cancellationToken = cancellationTokenSource.Token;
            var lastChecked = DateTime.Now;

            using var logDbContext = new LogDbContext();
            while (!cancellationToken.IsCancellationRequested)
            {
                foreach (var logModel in logDbContext.Log
                    .AsNoTracking()
                    .Where(n => n.SentDt > lastChecked)
                    .Where(n => customer.Equals("All") || ((n.CustomerId != null) && n.CustomerId.Equals(customer)))
                    .Where(n => ((int)n.Level & levelFilter) > 0))
                {
                    yield return logModel;
                }
                lastChecked = DateTime.Now;
                await Task.Delay(1000, cancellationToken);
            }
        }
    }
}
