using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Windows;
using System.Windows.Controls;

namespace FTO_App.Views
{
    public partial class LoginView : UserControl
    {
        public event EventHandler<string> OnLoginSuccess; // Evento para avisar MainWindow

        public LoginView()
        {
            InitializeComponent();
        }

        private void BtnLogin_Click(object sender, RoutedEventArgs e)
        {
            string u = TxtLoginUser.Text;
            string p = TxtLoginPass.Password;

            if (string.IsNullOrWhiteSpace(u) || string.IsNullOrWhiteSpace(p)) 
            { 
                MessageBox.Show("Preencha os campos.", "Erro", MessageBoxButton.OK, MessageBoxImage.Error); 
                return; 
            }

            try
            {
                using (var conn = Database.GetConnection())
                using (var cmd = new SQLiteCommand("SELECT Id FROM Users WHERE User = @u AND Senha = @p", conn))
                {
                    cmd.Parameters.AddWithValue("@u", u);
                    cmd.Parameters.AddWithValue("@p", p);
                    if (cmd.ExecuteScalar() != null)
                    {
                        LblWelcome.Text = $"Bem-vindo, {u}!";
                        LoginGrid.Visibility = Visibility.Collapsed;
                        SelectionGrid.Visibility = Visibility.Visible;
                    }
                    else 
                    {
                        MessageBox.Show("Dados incorretos.", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex) { MessageBox.Show($"Erro login: {ex.Message}"); }
        }

        private void BtnRegister_Click(object sender, RoutedEventArgs e)
        {
            string u = TxtRegUser.Text;
            string p = TxtRegPass.Password;
            if (string.IsNullOrWhiteSpace(u) || string.IsNullOrWhiteSpace(p)) return;

            try
            {
                Database.ExecuteNonQuery("INSERT INTO Users (User, Senha) VALUES (@u, @p)",
                    new Dictionary<string, object> {{"@u", u}, {"@p", p}});
                MessageBox.Show("Usuário criado!", "Sucesso", MessageBoxButton.OK, MessageBoxImage.Information);
                BtnVoltarLogin_Click(null, null);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro: {ex.Message}");
            }
        }

        private void BtnIrParaRegistro_Click(object sender, RoutedEventArgs e)
        {
            PanelLogin.Visibility = Visibility.Collapsed;
            PanelRegister.Visibility = Visibility.Visible;
            TxtRegUser.Focus();
        }

        private void BtnVoltarLogin_Click(object sender, RoutedEventArgs e)
        {
            PanelRegister.Visibility = Visibility.Collapsed;
            PanelLogin.Visibility = Visibility.Visible;
            TxtLoginUser.Focus();
        }

        private void BtnGoToSales_Click(object sender, RoutedEventArgs e)
        {
            // Dispara evento para a MainWindow trocar a tela
            OnLoginSuccess?.Invoke(this, TxtLoginUser.Text);
        }

        private void BtnLogout_Click(object sender, RoutedEventArgs e)
        {
            TxtLoginPass.Password = "";
            SelectionGrid.Visibility = Visibility.Collapsed;
            LoginGrid.Visibility = Visibility.Visible;
        }
    }
}