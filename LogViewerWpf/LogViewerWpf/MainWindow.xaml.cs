using LogWatcher.Models;
using Microsoft.AspNetCore.SignalR.Client;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
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
using static System.Net.WebRequestMethods;

namespace LogViewerWpf
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        HttpClient client;
        CancellationTokenSource connectionCancellationTokenSource = new CancellationTokenSource();
        CancellationToken connectionCancellationToken;
        CancellationTokenSource logUpdatesCancellationTokenSource = new CancellationTokenSource();
        CancellationToken logUpdatesCancellationToken;
        int maxLinesInListBoxFeed = 20;
        HubConnection connection;
        bool closing = false;

        public MainWindow()
        {
            client = new HttpClient();
            client.DefaultRequestHeaders.Accept.Clear();
            InitializeComponent();
            var uri = "https://localhost:7297/ReceiveLog";
            connection = new HubConnectionBuilder()
                .WithUrl(uri)
                .WithAutomaticReconnect()
                .Build();

            connection.Closed += (Exception? ex) =>
            {
                this.Dispatcher.Invoke(() =>
                {
                    textBoxStatus.Text = "Disconnected";
                    textBoxStatus.Foreground = Brushes.Red;
                    buttonStart.IsEnabled = false;
                    buttonStop.IsEnabled = false;
                });
                logUpdatesCancellationTokenSource.Cancel();
                if (!closing)
                {
                    ConnectToHub();
                }
                return Task.CompletedTask;
            };

            connection.Reconnecting += (Exception? ex) =>
            {
                this.Dispatcher.Invoke(() =>
                {
                    logUpdatesCancellationTokenSource.Cancel();
                    textBoxStatus.Text = "Reconnecting";
                    textBoxStatus.Foreground = Brushes.DarkGoldenrod;
                    buttonStart.IsEnabled = false;
                    buttonStop.IsEnabled = false;
                });
                return Task.CompletedTask;
            };

            connection.Reconnected += (string? s) =>
            {
                this.Dispatcher.Invoke(() =>
                {
                    textBoxStatus.Foreground = Brushes.Green;
                    textBoxStatus.Text = "Connected";
                    buttonStart.IsEnabled = true;
                    buttonStop.IsEnabled = false;
                }); return Task.CompletedTask;
            };
        }

        private void ConnectToHub()
        {
            while (true)
            {
                try
                {
                    connectionCancellationTokenSource = new CancellationTokenSource();
                    connectionCancellationToken = connectionCancellationTokenSource.Token;
                    connection.StartAsync(connectionCancellationToken);
                    this.Dispatcher.Invoke(async () =>
                    {
                        await LoadCustomers();
                        buttonStart.IsEnabled = true;
                        textBoxStatus.Text = "Connected";
                        textBoxStatus.Foreground = Brushes.Green;
                    });
                    UpdateFeed("LogHost connected.");
                    break;
                }
                catch when (connectionCancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    UpdateFeed("LogHost disconnected - " + ex.Message);
                    Task.Delay(1000);
                }
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            textBoxStatus.Text = "Disconnected";
            textBoxStatus.Foreground = Brushes.Red;
            comboBoxCustomers.IsEnabled = false;
            ConnectToHub();
        }

        private void UpdateFeed(string info, bool includeTimestamp = true)
        {
            Dispatcher.Invoke(async () =>
            {
                listBoxFeed.Items.Insert(0, includeTimestamp ? string.Format("{0:yyyy-MM-dd hh:mm:ss} - {1}", DateTime.Now, info) : info);
                if (listBoxFeed.Items.Count == maxLinesInListBoxFeed)
                {
                    listBoxFeed.Items.RemoveAt(maxLinesInListBoxFeed - 1);
                }
            });
        }

        private async Task LoadCustomers()
        {
            while (true)
            {
                try
                {

                    var url = "https://localhost:7297/customers";
                    var response = await client.GetAsync(url);
                    var content = await response.Content.ReadAsStreamAsync();
                    var contentString = await response.Content.ReadAsStringAsync();

                    var customers = new List<string>
                    {
                        "All"
                    };
                    customers.AddRange(JsonConvert.DeserializeObject<List<string>>(contentString) ?? new List<string>());
                    customers.ForEach(n => comboBoxCustomers.Items.Add(n));

                    comboBoxCustomers.IsEnabled = true;
                    comboBoxCustomers.SelectedIndex = 0;
                    return;
                }
                catch (Exception ex)
                {
                    UpdateFeed(ex.Message);
                }
                await Task.Delay(1000);
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            closing = true;
            logUpdatesCancellationTokenSource.Cancel();
            connectionCancellationTokenSource.Cancel();
        }

        private void StartReceiving(string customer)
        {
            Task.Run(async () =>
            {
                logUpdatesCancellationTokenSource = new CancellationTokenSource();
                logUpdatesCancellationToken = logUpdatesCancellationTokenSource.Token;
                var logModelStream = connection.StreamAsync<LogModel>("LogUpdates", customer, logUpdatesCancellationToken);
                await foreach (var model in logModelStream)
                {
                    {
                        var modelString = string.Format("{0:yyyy-MM-dd hh:mm:ss} - {1} {2} {3} {4}", model.CustomerId, model.SentDt, model.Module, model.Level, model.Message);
                        UpdateFeed(modelString, false);
                    }
                }
            });

        }

        private void buttonStart_Click(object sender, RoutedEventArgs e)
        {
            buttonStart.IsEnabled = false;
            buttonStop.IsEnabled = true;
            comboBoxCustomers.IsEnabled = false;
            string customer = comboBoxCustomers.SelectedItem.ToString() ?? "All";
            StartReceiving(customer);
        }

        private void buttonStop_Click(object sender, RoutedEventArgs e)
        {
            logUpdatesCancellationTokenSource.Cancel();
            buttonStop.IsEnabled = false;
            comboBoxCustomers.IsEnabled = true;
            buttonStart.IsEnabled = true;
        }
    }
}
