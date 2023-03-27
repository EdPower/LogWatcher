using LogWatcher.Models;
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace LogSenderWpf
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        HttpClient client;
        CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        Random rnd = new Random();

        public MainWindow()
        {
            InitializeComponent();
            client = new HttpClient();
            client.DefaultRequestHeaders.Accept.Clear();
        }

        private async void buttonStart_Click(object sender, RoutedEventArgs e)
        {
            cancellationTokenSource = new CancellationTokenSource();
            CancellationToken cancellationToken = cancellationTokenSource.Token;
            buttonStart.IsEnabled = false;
            buttonStop.IsEnabled = true;
            await StartSendingAsync(cancellationToken);
        }

        private void buttonStop_Click(object sender, RoutedEventArgs e)
        {
            cancellationTokenSource.Cancel();
            buttonStart.IsEnabled = true;
            buttonStop.IsEnabled = false;
        }

        private async Task StartSendingAsync(CancellationToken token)
        {
            var rnd = new Random();
            var url = "https://localhost:7297/add";
            while (!token.IsCancellationRequested)
            {
                var record = CreateRecord();
                var (isSuccessful, errorString) = await SendLogRecord(url, record, token);
                if (isSuccessful)
                {
                    AddStatus(record.SentDt.ToString("yyyy-MM-dd HH:mm:ss") + "  " + record.CustomerId + "  " + record.Level.ToString());
                }
                else
                {
                    AddStatus(record.SentDt.ToString("yyyy-MM-dd HH:mm:ss") + "  " + errorString);
                }
                await Task.Delay(rnd.Next(200, 2000));
            }
        }

        // add record to status listbox, keeping only last 20 records
        private void AddStatus(string status)
        {
            listBoxStatus.Items.Insert(0, status);
            if (listBoxStatus.Items.Count > 20)
            {
                listBoxStatus.Items.RemoveAt(20);
            }
        }

        // send log record to log host
        private async Task<(bool, string)> SendLogRecord(string url, LogModel record, CancellationToken token)
        {
            try
            {
                using HttpResponseMessage response = await client.PostAsync(url, JsonContent.Create(record), token);
                response.EnsureSuccessStatusCode();
                return (true, string.Empty);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        // create new record
        private LogModel CreateRecord()
        {
            var logModel = new LogModel()
            {
                CustomerId = GetRandomCustomerId(),
                Module = "module1",
                SentDt = DateTime.Now,
                Level = GetRandomLogLevel()
            };
            logModel.Message = GetMessage(logModel.Level);
            return logModel;
        }

        private string GetRandomCustomerId()
        {
            return "Customer-" + rnd.Next(1, 5).ToString();
        }

        private LogLevel GetRandomLogLevel()
        {
            return rnd.Next(1, 100) switch
            {
                > 0 and <= 15 => LogLevel.Error,
                > 15 and <= 35 => LogLevel.Warning,
                > 35 and <= 90 => LogLevel.Information,
                _ => LogLevel.Trace,
            };
        }

        private string GetMessage(LogLevel level)
        {
            switch (level)
            {
                case LogLevel.Information:
                    return "General information message";
                case LogLevel.Warning:
                    return "Alert - warning";
                case LogLevel.Error:
                    return "An error occured";
                case LogLevel.Trace:
                default:
                    return "Low-level trace data";
            }
        }
    }
}
