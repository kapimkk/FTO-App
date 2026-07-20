using FTO_App.Views;
using System;
using System.Windows;

namespace FTO_App
{
    public partial class MainWindow : Window
    {
        private LoginView _loginView;

        public MainWindow()
        {
            InitializeComponent();
            
            try 
            { 
                Database.InitTables(); 
            } 
            catch { } // Silencioso pois as views podem tratar erros pontuais

            InitLoginView();
        }

        private void InitLoginView()
        {
            _loginView = new LoginView();

            _loginView.OnLoginSuccess += (s, username) => 
            {
                this.Title = $"FTO - Painel de Vendas ({username})";
                ShowDashboard();
            };

            _loginView.OnEstoqueRequest += (s, e) =>
            {
                this.Title = "FTO - Módulo de Estoque";
                ShowEstoque();
            };

            _loginView.OnAnalyticsRequest += (s, e) =>
            {
                this.Title = "FTO - Painel Analítico";
                ShowAnalytics();
            };

            ShowLogin();
        }

        private void ShowLogin()
        {
            this.Title = "FTO - Painel de Acesso";
            MainContent.Content = _loginView;
        }

        private void ShowDashboard()
        {
            var dashboardView = new DashboardView();
            dashboardView.OnLogoutRequest += (s, e) => 
            {
                ShowLogin();
            };
            MainContent.Content = dashboardView;
        }

        private void ShowEstoque()
        {
            var estoqueView = new EstoqueView();
            estoqueView.OnBackRequest += (s, e) =>
            {
                ShowLogin();
            };
            MainContent.Content = estoqueView;
        }

        private void ShowAnalytics()
        {
            var analyticsView = new AnalyticsView();
            analyticsView.OnBackRequest += (s, e) =>
            {
                ShowLogin();
            };
            MainContent.Content = analyticsView;
        }
    }
}