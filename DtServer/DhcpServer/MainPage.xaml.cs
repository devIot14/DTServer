using DhcpServer.Model;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using System.Net;
using ServerTcp;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;

// Il modello di elemento Pagina vuota è documentato all'indirizzo https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x410

namespace DhcpServer
{
    /// <summary>
    /// Pagina vuota che può essere usata autonomamente oppure per l'esplorazione all'interno di un frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {

        private const int FLASH_TIME = 500;
        private const int DELAY = 10000;
        private const string RL = "\n";
        private const bool LOOP_SCORE = true;
        private const string DNS = "DomainIoT";
        private const string SUB_MASK = "255.255.0.0";
        private const string SERVER_IDENTIFIER = "169.254.139.40";
        private const string ROUTER_IP = "169.254.139.40";
        private const string AVVIA = "Avvia";
        private const string UPDATE = "Aggiorna";

        private static Server Server;

        private Dhcp.DhcpServer DhcpServer;

        public MainPage()
        {
            InitializeComponent();
            ViewModel = new UiBindingViewModel();
            Start();
        }

        public UiBindingViewModel ViewModel { get; set; }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Start();
        }

        private void Start()
        {
            if (this.StatusButton.Content.ToString().Equals(AVVIA))
            {
                this.ViewModel.Action = "AVVIO DEL SERVER DHCP IN CORSO...";
                RunServer();
                this.StatusButton.Content = UPDATE;
                Task.Run(async () => //Task.Run automatically unwraps nested Task types!
                {
                    await Task.Delay(DELAY);
                    Server = new Server();
                    Server.StartServer();
                });
            }

            var st = DhcpServer.Status;

            if (!(st is null) && st.Count > 0)
            {
                foreach (var s in st)
                {
                    ViewModel.Action = s;
                }

                DhcpServer.Readed = true;
                DhcpServer.UpdateReaded();
            }

            try
            {
                if (!(Server is null) && !(Server.Status is null) && Server.Status.Count > 0)
                {
                    foreach (var s in Server.Status)
                    {
                        ViewModel.Action = s;
                    }

                    Server.Status = new List<string>();
                }
            }
            catch (Exception e)
            {
                ViewModel.Action = "\t->\tÈ STATA GENERATA UN'ECCEZIONE NEL SERVER DHCP";
                ViewModel.Action = $"\t->\t{e.Message}";
                ViewModel.Action = $"\t->\t{e.StackTrace}";
            }
        }

        private void RunServer()
        {
            IPAddress iPAddress;

            DhcpServer = new Dhcp.DhcpServer();
            iPAddress = new IPAddress(new byte[] { 169, 254, 42, 91 });
            DhcpServer.Run(iPAddress, DNS, SUB_MASK, SERVER_IDENTIFIER, ROUTER_IP);
        }
    }
}
