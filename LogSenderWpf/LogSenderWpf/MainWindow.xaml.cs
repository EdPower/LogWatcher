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
            await StartSending(cancellationToken);
        }

        private void buttonStop_Click(object sender, RoutedEventArgs e)
        {
            cancellationTokenSource.Cancel();
            buttonStart.IsEnabled = true;
            buttonStop.IsEnabled = false;
        }

        private async Task StartSending(CancellationToken token)
        {
            var url = "https://localhost:7297/add";
            while (!token.IsCancellationRequested)
            {
                var record = CreateRecord();
                var (isSuccessful, errorString) = await SendLogRecord(url, record, token);
                if (isSuccessful)
                {
                    AddStatus(record.SentDt.ToString("yyyy-MM-dd HH:mm:ss") + " - " + record.Level.ToString());
                }
                else
                {
                    AddStatus(record.SentDt.ToString("yyyy-MM-dd HH:mm:ss") + " - " + errorString);
                }
                await Task.Delay(500);
            }
        }

        private void AddStatus(string status)
        {
            listBoxStatus.Items.Insert(0, status);
            if (listBoxStatus.Items.Count > 20)
            {
                listBoxStatus.Items.RemoveAt(19);
            }
        }

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

        private LogModel CreateRecord()
        {
            var logModel = new LogModel()
            {
                CustomerId = "cust1",
                Message = "test",
                Module = "module1",
                SentDt = DateTime.Now,
                Level = GetRandomLogLevel()
            };
            return logModel;
        }

        private static Random rnd = new Random();
        private LogLevel GetRandomLogLevel()
        {
            return rnd.Next(1, 100) switch
            {
                > 0 and <= 7 => LogLevel.Error,
                > 7 and <= 15 => LogLevel.Warning,
                > 15 and <= 90 => LogLevel.Information,
                _ => LogLevel.Trace,
            };
        }
    }
}
