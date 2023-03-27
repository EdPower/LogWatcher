using LogWatcher.Models;
using Microsoft.AspNetCore.SignalR.Client;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace LogViewerWpf
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        HttpClient client;
        CancellationTokenSource cancellationTokenSource = null!;
        CancellationToken cancellationToken;
        int maxLinesInListBoxFeed = 20;
        int maxLinesInListBoxError = 20;
        HubConnection connection = null!;
        volatile bool closing = false;
        Uri baseUri;

        public MainWindow()
        {
            client = new HttpClient();
            client.DefaultRequestHeaders.Accept.Clear();
            InitializeComponent();
            baseUri = new Uri("https://localhost:7297/ReceiveLog");
        }

        // connect to SignalR hub
        private void BuildConnection(string uri)
        {
            connection = new HubConnectionBuilder()
                .WithUrl(uri)
                .WithAutomaticReconnect()
                .Build();

            // update UI if hub connection is closed
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

            // update UI when reconnecting to hub
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

            // update UI when hub is reconnected
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

            // try to connect to hub and periodically check to see if connection was dropped
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

                        if (!await ConnectToHubAsync())
                        {
                            // wait a second before trying to connect again
                            await Task.Delay(1000);
                        }
                    }
                    else if (connection.State == HubConnectionState.Connected)
                    {
                        // wait five seconds before checking to see if disconnected
                        await Task.Delay(5000);
                    }
                }
            });
        }

        // Attempt to make the actual connection
        private async Task<bool> ConnectToHubAsync()
        {
            while (true)
            {
                try
                {
                    // try the connection
                    await connection.StartAsync();
                    var attempCount = 0;
                    var maxAttempts = 20;

                    // loop while connecting up to maxAttempts attempts
                    while (connection.State == HubConnectionState.Connecting && attempCount < maxAttempts)
                    {
                        await Task.Delay(100);
                        attempCount++;
                    }

                    // if connected before reaching maxAttempts then update customer list and return success
                    if (connection.State == HubConnectionState.Connected && attempCount < maxAttempts)
                    {
                        await LoadCustomersAsync();
                        this.Dispatcher.Invoke(() =>
                        {
                            buttonStart.IsEnabled = true;
                            textBoxStatus.Text = "Connected";
                            textBoxStatus.Foreground = Brushes.Green;
                        });
                        UpdateFeed("LogHost connected.");
                        return true;
                    }
                    // otherwise return failed to connect
                    return false;
                }
                catch (Exception ex)
                {
                    UpdateFeed("LogHost connection error: " + ex.Message);
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

        // update feed listbox with model record
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

        // update feed listbox with info string
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

        // update error listbox
        private void UpdateErrors(LogModel model)
        {
            Dispatcher.Invoke(() =>
            {
                var modelString = string.Format("{0:yyyy-MM-dd HH:mm:ss} - {1} {2} {3}", model.CustomerId, model.SentDt, model.Module, model.Message);
                listBoxErrors.Items.Insert(0, new ListBoxItem()
                {
                    Content = modelString,
                    Tag = model,
                    FontWeight = model.Level == LogLevel.Error ? FontWeights.Bold : FontWeights.Normal
                }); ;

                if (listBoxErrors.Items.Count == maxLinesInListBoxError)
                {
                    listBoxErrors.Items.RemoveAt(maxLinesInListBoxError - 1);
                }
            });
        }

        // load customer combo box
        private async Task LoadCustomersAsync()
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
                // wait a second and try again
                await Task.Delay(1000);
            }
        }

        // signal hub to stop sending updates when shutting down
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            closing = true;
            if (connection.State == HubConnectionState.Connected)
            {
                connection.InvokeAsync("StopLogUpdates");
            }
        }

        // tell hub to start sending updates
        private async Task ReceiveFeedAsync(string customer, int minLevel)
        {
            var logModelStream = connection.StreamAsync<LogModel>("GetLogUpdates", customer, minLevel);
            await foreach (var model in logModelStream)
            {
                UpdateFeed(model);
            }
        }

        // poll host for recently uploaded warnings and errors
        private async Task GetLastWarningsAndErrorsAsync(string customer, CancellationToken cancellationToken, int maxRecordsToTake)
        {
            // start by looking for anything in the last 24 hours
            // but only get last maxRecordsToTake records (saves downloading a large amount of records just to get lastest)
            var lastChecked = DateTime.Now.AddDays(-1);

            var uriBuilder = new UriBuilder(new Uri(baseUri, "since"));
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // build list of parameters
                    var kvps = new List<KeyValuePair<string, string>>
                        {
                            new KeyValuePair<string, string>("fromDt", lastChecked.ToString("yyyy-MM-ddTHH:mm:ss")),
                            new KeyValuePair<string, string>("logLevel",((int)LogLevel.Warning).ToString()),
                            new KeyValuePair<string, string>("customerId", customer ),
                            new KeyValuePair<string, string>("lastCount", maxRecordsToTake.ToString())
                        };

                    // build query from parameter list
                    uriBuilder.Query = string.Join("&", kvps.Select(n => string.Join("=", n.Key, n.Value)));

                    // query host
                    var response = await client.GetAsync(uriBuilder.Uri.AbsoluteUri);

                    // extract response contents
                    var content = await response.Content.ReadAsStreamAsync();
                    var contentString = await response.Content.ReadAsStringAsync();
                    if (!string.IsNullOrEmpty(contentString))
                    {
                        // convert string to json to extract model records and update error listbox
                        foreach (var model in JsonConvert.DeserializeObject<List<LogModel>>(contentString) ?? new List<LogModel>())
                        {
                            this.Dispatcher.Invoke(() =>
                                {
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
                // wait 2 seconds and check again
                await Task.Delay(2000);
            }
        }

        private void buttonStart_Click(object sender, RoutedEventArgs e)
        {
            // update UI
            buttonStart.IsEnabled = false;
            buttonStop.IsEnabled = true;
            comboBoxCustomers.IsEnabled = false;
            checkBoxError.IsEnabled = false;
            checkBoxWarning.IsEnabled = false;
            checkBoxInformation.IsEnabled = false;
            checkBoxTrace.IsEnabled = false;

            // start receiving feed
            string customer = comboBoxCustomers.SelectedItem.ToString() ?? "All";
            int minLevel = checkBoxTrace.IsChecked ?? false ? (int)LogLevel.Trace : 0;
            minLevel += checkBoxInformation.IsChecked ?? false ? (int)LogLevel.Information : 0;
            minLevel += checkBoxWarning.IsChecked ?? false ? (int)LogLevel.Warning : 0;
            minLevel += checkBoxError.IsChecked ?? false ? (int)LogLevel.Error : 0;
            Task.Run(async () =>
            {
                await ReceiveFeedAsync(customer, minLevel);
            });

            // start receiving latest warnings and errors
            cancellationTokenSource = new CancellationTokenSource();
            cancellationToken = cancellationTokenSource.Token;
            Task.Run(async () =>
            {
                await GetLastWarningsAndErrorsAsync(customer, cancellationToken, maxLinesInListBoxError);
            }, cancellationToken);
        }

        private void buttonStop_Click(object sender, RoutedEventArgs e)
        {
            // cancel feed and error updates
            cancellationTokenSource.Cancel();
            connection.InvokeAsync("StopLogUpdates");

            // update UI
            buttonStop.IsEnabled = false;
            comboBoxCustomers.IsEnabled = true;
            buttonStart.IsEnabled = true;
            checkBoxTrace.IsEnabled = true;
            checkBoxInformation.IsEnabled = true;
            checkBoxWarning.IsEnabled = true;
            checkBoxError.IsEnabled = true;
        }

        //  update textblock when clicking on listBoxErrors item
        private void listBoxErrors_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedItem = listBoxErrors.SelectedItem as ListBoxItem;
            if (selectedItem != null && selectedItem.Tag != null)
            {
                var tag = selectedItem.Tag as LogModel;
                textBlockErrorRecord.Text = tag?.Message;
            }
        }

        //  update textblock when clicking on listBoxFeed item
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
