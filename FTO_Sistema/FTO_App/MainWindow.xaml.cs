using FTO_App.Views;
using System;
using System.Windows;

namespace FTO_App
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            
            try 
            { 
                Database.InitTables(); 
            } 
            catch { } // Silencioso pois as views podem tratar erros pontuais

            ShowLogin();
        }

        private void ShowLogin()
        {
            var loginView = new LoginView();
            // Quando o login der certo, troca para o Dashboard
            loginView.OnLoginSuccess += (s, username) => 
            {
                this.Title = $"FTO - Painel de Vendas ({username})";
                ShowDashboard();
            };
            MainContent.Content = loginView;
        }

        private void ShowDashboard()
        {
            var dashboardView = new DashboardView();
            // Quando clicar em "Voltar", volta para o Login
            dashboardView.OnLogoutRequest += (s, e) => 
            {
                this.Title = "FTO - Painel de Acesso";
                ShowLogin();
            };
            MainContent.Content = dashboardView;
        }
    }
}