using FTO_App.Views;
using System.Windows;

namespace FTO_App
{
    public partial class MainWindow : Window
    {
        private LoginView _loginView = null!;

        public MainWindow()
        {
            // Database.InitTables()/EmpresaConfigStore.Load() já rodaram em App.OnStartup — se falharem,
            // o app é encerrado antes desta janela ser criada, então chegar aqui já garante banco pronto.
            InitializeComponent();
            InitLoginView();
        }

        private void InitLoginView()
        {
            _loginView = new LoginView();
            _loginView.OnLoginSuccess += (s, username) =>
            {
                Title = $"FTO Sistemas — {username}";
                ShowShell(username);
            };
            ShowLogin();
        }

        private void ShowLogin()
        {
            Title = "FTO - Painel de Acesso";
            MainContent.Content = _loginView;
        }

        private void ShowShell(string username)
        {
            var shell = new MainShellView(username);
            shell.OnLogoutRequest += (_, _) => ShowLogin();
            MainContent.Content = shell;
        }
    }
}
