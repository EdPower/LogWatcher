using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using LogWatcher;
using Microsoft.AspNetCore.SignalR.Client;
using System.Windows.Media.Animation;

namespace LogSenderWpf
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        HttpClient client;
        bool isSending = false;

        public MainWindow()
        {
            InitializeComponent();
            client = new HttpClient();
            client.DefaultRequestHeaders.Accept.Clear();
        }

        private void buttonStart_Click(object sender, RoutedEventArgs e)
        {
            isSending = true;
        }

        private void buttonStop_Click(object sender, RoutedEventArgs e)
        {
            isSending = false;
        }

        private async Task StartSending()
        {
            var url = "https://localhost:7297/add";
            while (isSending)
            {
                var record = CreateRecord();
                var result = SendLogRecord(url, record).Result;
                if (result != null)
                {
                    textBoxStatus.Text += result + Environment.NewLine;
                }
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
                Level = LogLevel.Information
            };
            return logModel;
        }

        private async Task<string> SendLogRecord(string url, LogModel record)
        {
            using HttpResponseMessage response = await client.PostAsync(url, JsonContent.Create(record));

            response.EnsureSuccessStatusCode();
            var responseContent = await response.Content.ReadAsStringAsync();

            return responseContent;
        }
    }
}