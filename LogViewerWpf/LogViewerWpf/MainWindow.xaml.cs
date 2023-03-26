using LogWatcher.Models;
using Microsoft.AspNetCore.SignalR.Client;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Runtime.Serialization.Json;
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
        CancellationTokenSource cancellationTokenSource;
        CancellationToken cancellationToken;
        int maxLinesInListBoxFeed = 20;
        int maxLinesInListBoxError = 20;
        HubConnection connection;
        bool closing = false;
        Uri baseUri;

        public MainWindow()
        {
            client = new HttpClient();
            client.DefaultRequestHeaders.Accept.Clear();
            InitializeComponent();
            baseUri = new Uri("https://localhost:7297/ReceiveLog");
        }

        private void BuildConnection(string uri)
        {
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

                return Task.CompletedTask;
            };

            connection.Reconnecting += (Exception? ex) =>
            {
                this.Dispatcher.Invoke(() =>
                {
                    //logUpdatesCancellationTokenSource.Cancel();
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
                });
                return Task.CompletedTask;
            };

            Task.Run(async () =>
            {
                while (!closing)
                {
                    if (connection.State == HubConnectionState.Disconnected)
                    {
                        this.Dispatcher.Invoke(() =>
                        {
                            textBoxStatus.Text = "Disconnected";
                            textBoxStatus.Foreground = Brushes.Red;
                            buttonStart.IsEnabled = false;
                            buttonStop.IsEnabled = false;
                        });

                        if (!await ConnectToHub())
                        {
                            await Task.Delay(1000);
                        }
                    }
                    else if (connection.State == HubConnectionState.Connected)
                    {
                        await Task.Delay(5000);
                    }
                }
            });
        }

        private async Task<bool> ConnectToHub()
        {
            while (true)
            {
                try
                {
                    await connection.StartAsync();
                    var delayCount = 0;
                    var maxDelay = 20;
                    while (connection.State == HubConnectionState.Connecting && delayCount < maxDelay)
                    {
                        await Task.Delay(100);
                        delayCount++;
                    }
                    if (connection.State == HubConnectionState.Connected && delayCount < maxDelay)
                    {
                        await LoadCustomers();
                        this.Dispatcher.Invoke(() =>
                        {
                            buttonStart.IsEnabled = true;
                            textBoxStatus.Text = "Connected";
                            textBoxStatus.Foreground = Brushes.Green;
                        });
                        UpdateFeed("LogHost connected.");
                        return true;
                    }
                    return false;
                }
                catch (Exception ex)
                {
                    UpdateFeed("LogHost disconnected - " + ex.Message);
                    return false;
                }
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            textBoxStatus.Text = "Disconnected";
            textBoxStatus.Foreground = Brushes.Red;
            comboBoxCustomers.IsEnabled = false;
            BuildConnection(baseUri.ToString());
        }

        private void UpdateFeed(LogModel model)
        {
            var modelString = string.Format("{0:yyyy-MM-dd HH:mm:ss} - {1} {2} {3}: {4}", model.CustomerId, model.SentDt, model.Module, model.Level, model.Message);
            Dispatcher.Invoke(() =>
            {
                listBoxFeed.Items.Insert(0, new ListBoxItem() { Content = modelString, Tag = model });

                if (listBoxFeed.Items.Count == maxLinesInListBoxError)
                {
                    listBoxFeed.Items.RemoveAt(maxLinesInListBoxError - 1);
                }
            });
        }

        private void UpdateFeed(string info)
        {
            Dispatcher.Invoke(() =>
            {
                listBoxFeed.Items.Insert(0, info);
                if (listBoxFeed.Items.Count == maxLinesInListBoxFeed)
                {
                    listBoxFeed.Items.RemoveAt(maxLinesInListBoxFeed - 1);
                }
            });
        }

        private void UpdateErrors(LogModel model)
        {
            Dispatcher.Invoke(() =>
            {
                var modelString = string.Format("{0:yyyy-MM-dd HH:mm:ss} - {1} {2} {3}", model.CustomerId, model.SentDt, model.Module, model.Message);
                listBoxErrors.Items.Insert(0, new ListBoxItem() { Content = modelString, Tag = model, Foreground = model.Level == LogLevel.Error ? Brushes.Red : Brushes.Black });

                if (listBoxErrors.Items.Count == maxLinesInListBoxError)
                {
                    listBoxErrors.Items.RemoveAt(maxLinesInListBoxError - 1);
                }
            });
        }

        private async Task LoadCustomers()
        {
            while (true)
            {
                try
                {
                    var uri = new Uri(baseUri, "customers");
                    var response = await client.GetAsync(uri.ToString());
                    var content = await response.Content.ReadAsStreamAsync();
                    var contentString = await response.Content.ReadAsStringAsync();
                    if (!string.IsNullOrEmpty(contentString))
                    {
                        var customers = new List<string> { "All" };
                        customers.AddRange(JsonConvert.DeserializeObject<List<string>>(contentString) ?? new List<string>());
                        this.Dispatcher.Invoke(() =>
                        {
                            customers.ForEach(n => comboBoxCustomers.Items.Add(n));
                            comboBoxCustomers.IsEnabled = true;
                            comboBoxCustomers.SelectedIndex = 0;
                        });
                    }
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
            if (connection.State == HubConnectionState.Connected)
            {
                connection.InvokeAsync("StopLogUpdates");
            }
        }

        private async Task ReceiveFeed(string customer, int minLevel)
        {
            var logModelStream = connection.StreamAsync<LogModel>("GetLogUpdates", customer, minLevel);
            await foreach (var model in logModelStream)
            {
                UpdateFeed(model);
            }
        }

        private async Task GetLastWarningsAndErrors(string customer, CancellationToken cancellationToken)
        {
            var uriBuilder = new UriBuilder(new Uri(baseUri, "since"));
            var lastChecked = DateTime.Now.AddDays(-1);
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var kvps = new List<KeyValuePair<string, string>>
                        {
                            new KeyValuePair<string, string>("fromDt", lastChecked.ToString("yyyy-MM-ddTHH:mm:ss")),
                            new KeyValuePair<string, string>("logLevel",((int)LogLevel.Warning).ToString()),
                            new KeyValuePair<string, string>("customerId", customer )
                        };

                    uriBuilder.Query = string.Join("&", kvps.Select(n => string.Join("=", n.Key, n.Value)));
                    var response = await client.GetAsync(uriBuilder.Uri.AbsoluteUri);

                    var content = await response.Content.ReadAsStreamAsync();
                    var contentString = await response.Content.ReadAsStringAsync();
                    if (!string.IsNullOrEmpty(contentString))
                    {
                        foreach (var model in JsonConvert.DeserializeObject<List<LogModel>>(contentString) ?? new List<LogModel>())
                        {
                            this.Dispatcher.Invoke(() =>
                                {
                                    var modelString = string.Format("{0:yyyy-MM-dd HH:mm:ss} - {1} {2} {3}", model.CustomerId, model.SentDt, model.Module, model.Message);
                                    UpdateErrors(model);
                                });
                        }
                    }
                    lastChecked = DateTime.Now;
                }
                catch (Exception ex)
                {
                    UpdateFeed(ex.Message);
                }
                await Task.Delay(2000);
            }
        }

        private void buttonStart_Click(object sender, RoutedEventArgs e)
        {
            buttonStart.IsEnabled = false;
            buttonStop.IsEnabled = true;
            comboBoxCustomers.IsEnabled = false;
            checkBoxError.IsEnabled = false;
            checkBoxWarning.IsEnabled = false;
            checkBoxInformation.IsEnabled = false;
            checkBoxTrace.IsEnabled = false;
            string customer = comboBoxCustomers.SelectedItem.ToString() ?? "All";
            int minLevel = checkBoxTrace.IsChecked ?? false ? (int)LogLevel.Trace : 0;
            minLevel += checkBoxInformation.IsChecked ?? false ? (int)LogLevel.Information : 0;
            minLevel += checkBoxWarning.IsChecked ?? false ? (int)LogLevel.Warning : 0;
            minLevel += checkBoxError.IsChecked ?? false ? (int)LogLevel.Error : 0;

            Task.Run(async () => { await ReceiveFeed(customer, minLevel); });

            cancellationTokenSource = new CancellationTokenSource();
            cancellationToken = cancellationTokenSource.Token;
            Task.Run(async () => { await GetLastWarningsAndErrors(customer, cancellationToken); }, cancellationToken);
        }

        private void buttonStop_Click(object sender, RoutedEventArgs e)
        {
            cancellationTokenSource.Cancel();
            connection.InvokeAsync("StopLogUpdates");
            buttonStop.IsEnabled = false;
            comboBoxCustomers.IsEnabled = true;
            buttonStart.IsEnabled = true;
            checkBoxTrace.IsEnabled = true;
            checkBoxInformation.IsEnabled = true;
            checkBoxWarning.IsEnabled = true;
            checkBoxError.IsEnabled = true;
        }

        private void listBoxErrors_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedItem = listBoxErrors.SelectedItem as ListBoxItem;
            if (selectedItem != null && selectedItem.Tag != null)
            {
                var tag = selectedItem.Tag as LogModel;
                textBlockErrorRecord.Text = tag?.Message;
            }
        }

        private void listBoxFeed_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedItem = listBoxFeed.SelectedItem as ListBoxItem;
            if (selectedItem != null && selectedItem.Tag != null)
            {
                var tag = selectedItem.Tag as LogModel;
                textBlockFeedRecord.Text = tag?.Message;
            }
        }
    }
}
