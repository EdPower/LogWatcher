using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
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
using static System.Net.WebRequestMethods;

namespace LogViewerWpf
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        HttpClient client;

        public MainWindow()
        {
            client = new HttpClient();
            client.DefaultRequestHeaders.Accept.Clear();
            InitializeComponent();
            var customers = GetCustomers();
            comboBoxCustomers.Items.Add("All");
            foreach ( var customer in customers )
            {
                comboBoxCustomers.Items.Add( customer );
            }
            comboBoxCustomers.SelectedIndex = 0;
        }

        private List<string> GetCustomers()
        {
            var url = "https://localhost:7297/customers";
            var response = client.GetAsync(url).Result;
            var content = response.Content.ReadAsStreamAsync();
            var contentString = response.Content.ReadAsStringAsync().Result;
            return JsonConvert.DeserializeObject<List<string>>(contentString) ?? new List<string>();
        }
    }
}
