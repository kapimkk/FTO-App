using System;
using System.Windows;
using System.Windows.Controls;

namespace FTO_App.Views
{
    public partial class MainShellView : UserControl
    {
        public event EventHandler? OnLogoutRequest;

        private readonly string _username;

        private DashboardView? _vendas;
        private AnalyticsView? _dashboard;
        private EstoqueView? _estoque;
        private ClientesView? _clientes;
        private NotaFiscalView? _notaFiscal;
        private ConfiguracoesView? _config;

        public MainShellView(string username)
        {
            InitializeComponent();
            _username = username;
            LblUsuario.Text = string.IsNullOrWhiteSpace(username) ? "" : $"Olá, {username}";
            Loaded += (_, _) => Navegar("vendas");
        }

        private void BtnSair_Click(object sender, RoutedEventArgs e)
            => OnLogoutRequest?.Invoke(this, EventArgs.Empty);

        private void Nav_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string tag)
                Navegar(tag);
        }

        private void Navegar(string modulo)
        {
            HighlightNav(modulo);

            switch (modulo)
            {
                case "vendas":
                    _vendas ??= CreateVendas();
                    ModuleContent.Content = _vendas;
                    break;
                case "dashboard":
                    _dashboard ??= new AnalyticsView();
                    ModuleContent.Content = _dashboard;
                    break;
                case "estoque":
                    _estoque ??= new EstoqueView();
                    ModuleContent.Content = _estoque;
                    break;
                case "clientes":
                    _clientes ??= new ClientesView();
                    ModuleContent.Content = _clientes;
                    break;
                case "nfe":
                    _notaFiscal ??= new NotaFiscalView();
                    ModuleContent.Content = _notaFiscal;
                    break;
                case "config":
                    _config ??= new ConfiguracoesView();
                    ModuleContent.Content = _config;
                    break;
            }
        }

        private DashboardView CreateVendas()
        {
            var view = new DashboardView();
            view.SetEmbeddedMode(true);
            return view;
        }

        private void HighlightNav(string modulo)
        {
            void Style(Button? b, bool on)
            {
                if (b == null) return;
                b.Background = on
                    ? (System.Windows.Media.Brush)FindResource("PrimaryColor")
                    : System.Windows.Media.Brushes.Transparent;
                b.Foreground = System.Windows.Media.Brushes.White;
            }

            Style(BtnNavVendas, modulo == "vendas");
            Style(BtnNavDashboard, modulo == "dashboard");
            Style(BtnNavEstoque, modulo == "estoque");
            Style(BtnNavClientes, modulo == "clientes");
            Style(BtnNavNotaFiscal, modulo == "nfe");
            Style(BtnNavConfig, modulo == "config");
        }
    }
}
