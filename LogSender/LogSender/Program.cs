// See https://aka.ms/new-console-template for more information
using LogWatcher;
using Microsoft.AspNetCore.SignalR.Client;
using System.Net.Http.Json;

var client = new HttpClient();

client.DefaultRequestHeaders.Accept.Clear();

await AddRecords(100, 100);

await ExecuteAsync();

static async Task ExecuteAsync()
{
    var uri = "https://localhost:7297/current-time";
    await using var connection = new HubConnectionBuilder().WithUrl(uri).Build();
    await connection.StartAsync();

    await foreach (var date in connection.StreamAsync<DateTime>("Streaming"))
    {
        Console.WriteLine(date);
    }
}

async Task AddRecords(int count, int delayMs)
{
    for (int i = 0; i < count; i++)
    {
        var url = "https://localhost:7297/add";
        var logModel = new LogModel() { CustomerId = "cust1", Message = "test", Module = "module1", SentDt = DateTime.Now, Level = LogLevel.Information };

        using HttpResponseMessage response = await client.PostAsync(url, JsonContent.Create(logModel));

        response.EnsureSuccessStatusCode();
        var responseContent = await response.Content.ReadAsStringAsync();

        Console.WriteLine(responseContent);

        Task.Delay(delayMs);
    }
}