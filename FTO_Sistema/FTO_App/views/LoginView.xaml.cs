using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using FTO_App.Services;

namespace FTO_App.Views
{
    public partial class LoginView : UserControl
    {
        public event EventHandler<string> OnLoginSuccess; // Evento para avisar MainWindow
        public event EventHandler OnEstoqueRequest;
        public event EventHandler OnAnalyticsRequest;
        private bool _suppressDeviceSave;
        private bool _updateInProgress;

        public LoginView()
        {
            InitializeComponent();
            LblVersao.Text = $"Versão {UpdateService.GetLocalVersionDisplay()}";
        }

        private async void BtnAtualizarSistema_Click(object sender, RoutedEventArgs e)
        {
            if (_updateInProgress) return;

            _updateInProgress = true;
            BtnAtualizarSistema.IsEnabled = false;
            string originalLabel = BtnAtualizarSistema.Content?.ToString() ?? "↻ Atualizar sistema";

            try
            {
                BtnAtualizarSistema.Content = "Verificando...";
                UpdateCheckResult check = await UpdateService.CheckForUpdateAsync();

                if (!check.Success)
                {
                    MessageBox.Show(
                        check.ErrorMessage ?? "Não foi possível verificar atualizações.",
                        "Atualização",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                if (!check.UpdateAvailable)
                {
                    string msg = string.IsNullOrWhiteSpace(check.ErrorMessage)
                        ? $"Você já está na versão mais recente.\n\nVersão atual: {check.LocalVersionDisplay}"
                        : check.ErrorMessage;
                    MessageBox.Show(msg, "Atualização", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                string notes = string.IsNullOrWhiteSpace(check.ReleaseNotes)
                    ? ""
                    : $"\n\nNotas:\n{Truncate(check.ReleaseNotes, 400)}";

                var confirm = MessageBox.Show(
                    $"Nova versão disponível: {check.RemoteVersionDisplay}\n" +
                    $"Versão instalada: {check.LocalVersionDisplay}\n\n" +
                    $"O sistema será fechado, atualizado e reaberto automaticamente." +
                    $"\nSeus dados (.env e banco) serão preservados.{notes}",
                    "Atualizar sistema",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (confirm != MessageBoxResult.Yes)
                    return;

                var progress = new Progress<string>(status =>
                {
                    BtnAtualizarSistema.Content = status;
                });

                await UpdateService.DownloadAndPrepareUpdateAsync(check, progress);

                MessageBox.Show(
                    "Download concluído. O aplicativo será fechado para aplicar a atualização.",
                    "Atualização",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Falha ao atualizar:\n\n{ex.Message}",
                    "Atualização",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                _updateInProgress = false;
                BtnAtualizarSistema.Content = originalLabel;
                BtnAtualizarSistema.IsEnabled = true;
            }
        }

        private static string Truncate(string text, int max)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= max)
                return text;
            return text[..max] + "...";
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
                        LoadDeviceLists();
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
            OnLoginSuccess?.Invoke(this, TxtLoginUser.Text);
        }

        private void BtnGoToEstoque_Click(object sender, RoutedEventArgs e)
        {
            OnEstoqueRequest?.Invoke(this, EventArgs.Empty);
        }

        private void BtnGoToAnalytics_Click(object sender, RoutedEventArgs e)
        {
            OnAnalyticsRequest?.Invoke(this, EventArgs.Empty);
        }

        private void BtnLogout_Click(object sender, RoutedEventArgs e)
        {
            TxtLoginPass.Password = "";
            SelectionGrid.Visibility = Visibility.Collapsed;
            LoginGrid.Visibility = Visibility.Visible;
        }

        private void LoadDeviceLists()
        {
            _suppressDeviceSave = true;
            try
            {
                var printers = InstalledDevicesService.GetPrinters();
                CbImpressora.ItemsSource = printers;

                if (!string.IsNullOrWhiteSpace(DeviceSettingsStore.Current.SelectedPrinter) &&
                    printers.Contains(DeviceSettingsStore.Current.SelectedPrinter))
                {
                    CbImpressora.SelectedItem = DeviceSettingsStore.Current.SelectedPrinter;
                }
                else
                {
                    string? preferred = InstalledDevicesService.FindPreferredThermalPrinter(printers);
                    if (preferred != null)
                    {
                        CbImpressora.SelectedItem = preferred;
                        DeviceSettingsStore.SetPrinter(preferred);
                    }
                    else if (printers.Count > 0)
                        CbImpressora.SelectedIndex = 0;
                    else
                        CbImpressora.Items.Add("(Nenhuma impressora encontrada)");
                }

                var scanners = InstalledDevicesService.GetScanners();
                CbScanner.ItemsSource = scanners;

                if (!string.IsNullOrWhiteSpace(DeviceSettingsStore.Current.SelectedScanner) &&
                    scanners.Any(s => s.Equals(DeviceSettingsStore.Current.SelectedScanner, StringComparison.OrdinalIgnoreCase)))
                {
                    CbScanner.SelectedItem = scanners.First(s =>
                        s.Equals(DeviceSettingsStore.Current.SelectedScanner, StringComparison.OrdinalIgnoreCase));
                }
                else if (scanners.Count > 0)
                    CbScanner.SelectedIndex = 0;
            }
            finally
            {
                _suppressDeviceSave = false;
            }
        }

        private void BtnRefreshDevices_Click(object sender, RoutedEventArgs e)
        {
            LoadDeviceLists();
            MessageBox.Show("Lista de dispositivos atualizada.", "Dispositivos", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void CbImpressora_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressDeviceSave || CbImpressora.SelectedItem is not string printer) return;
            if (printer.StartsWith("(", StringComparison.Ordinal)) return;
            try { DeviceSettingsStore.SetPrinter(printer); }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Erro", MessageBoxButton.OK, MessageBoxImage.Warning); }
        }

        private void CbScanner_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressDeviceSave || CbScanner.SelectedItem is not string scanner) return;
            if (scanner.StartsWith("(", StringComparison.Ordinal)) return;
            try { DeviceSettingsStore.SetScanner(scanner); }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Erro", MessageBoxButton.OK, MessageBoxImage.Warning); }
        }
    }
}