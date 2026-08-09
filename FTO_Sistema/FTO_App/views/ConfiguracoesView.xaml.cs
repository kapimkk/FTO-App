using FTO_App.Models;
using FTO_App.Services;
using Microsoft.Win32;
using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace FTO_App.Views
{
    public partial class ConfiguracoesView : UserControl
    {
        public ConfiguracoesView()
        {
            InitializeComponent();
            Loaded += (_, _) => Carregar();
        }

        private void Carregar()
        {
            var c = EmpresaConfigStore.Current;
            TxtNome.Text = c.Nome;
            TxtSubtitulo.Text = c.Subtitulo;
            TxtRazao.Text = c.RazaoSocial;
            TxtFantasia.Text = c.NomeFantasia;
            TxtCnpj.Text = c.Cnpj;
            TxtIe.Text = c.Ie;
            TxtIm.Text = c.Im;
            TxtCnae.Text = c.Cnae;
            TxtTelefone.Text = c.Telefone;
            TxtEmail.Text = c.Email;
            TxtEndereco.Text = c.Endereco;
            TxtNumero.Text = c.Numero;
            TxtComplemento.Text = c.Complemento;
            TxtBairro.Text = c.Bairro;
            TxtCidade.Text = c.Cidade;
            TxtUf.Text = c.Uf;
            TxtCep.Text = c.Cep;
            TxtIbge.Text = c.CodigoIbge;
            TxtSerieNfe.Text = c.SerieNfe;
            TxtUltimoNfe.Text = c.UltimoNumeroNfe;
            TxtCscIdHomolog.Text = c.CscIdHomologacao;
            TxtCscTokenHomolog.Text = c.CscTokenHomologacao;
            TxtCscIdProducao.Text = c.CscIdProducao;
            TxtCscTokenProducao.Text = c.CscTokenProducao;
            TxtFiscalUrlNfe.Text = c.FiscalApiUrlNfe;
            TxtFiscalUrlNfce.Text = c.FiscalApiUrlNfce;
            TxtFiscalApiKey.Text = c.FiscalApiKey;
            TxtLogoPath.Text = c.LogoPath;
            TxtCupomTitulo.Text = c.CupomTitulo;
            TxtCupomRodape.Text = c.CupomRodape;

            CbRegime.SelectedIndex = c.RegimeTributario switch { "2" => 1, "3" => 2, _ => 0 };
            CbAmbiente.SelectedIndex = c.AmbienteNfe == "1" ? 0 : 1;

            CbIbsCbsPreset.SelectedIndex = c.IbsCbsPreset switch
            {
                "projetado" => 1,
                "personalizado" => 2,
                _ => 0
            };
            ChkIbsCbsAuto.IsChecked = c.IbsCbsCalculoAutomatico;
            ChkIbsCbsDestaque.IsChecked = c.IbsCbsDestaqueObrigatorio;
            TxtCbsAliq.Text = c.CbsAliquota.ToString("0.####");
            TxtIbsAliq.Text = c.IbsAliquota.ToString("0.####");
            TxtIbsUfAliq.Text = c.IbsAliquotaUf.ToString("0.####");
            TxtIbsMunAliq.Text = c.IbsAliquotaMun.ToString("0.####");
            AtualizarSimulacaoIbsCbs();

            LblEnvPath.Text = "Dados da empresa e fiscais: PostgreSQL (tabela empresa_config)";
            MostrarLogo(c.LogoPath);
            LoadDevices();
            AtualizarStatusBanco();
        }

        private void AtualizarStatusBanco()
        {
            try
            {
                Database.ReloadConnectionString();
                using var conn = Database.GetConnection();
                LblPgStatus.Text = "✅ Conectado ao PostgreSQL.";
                LblPgStatus.Foreground = System.Windows.Media.Brushes.SeaGreen;
            }
            catch (Exception ex)
            {
                LblPgStatus.Text = "❌ " + ex.Message;
                LblPgStatus.Foreground = System.Windows.Media.Brushes.IndianRed;
            }

            LblSqlitePath.Text = $"SQLite legado (se existir): {Database.SqliteLegacyPath}";
        }

        private void BtnTestPg_Click(object sender, RoutedEventArgs e)
        {
            AtualizarStatusBanco();
            MessageBox.Show(LblPgStatus.Text, "PostgreSQL", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnMigrarSqlite_Click(object sender, RoutedEventArgs e)
        {
            var confirm = MessageBox.Show(
                "Isso vai TRUNCAR as tabelas no PostgreSQL e recopiar tudo do FTO.db\n" +
                "com valores monetários corrigidos (formato 160.00 ≠ milhar).\n\n" +
                "Use se o dashboard/vendas estiver com valores ×100 (ex.: milhões).\n" +
                "O arquivo SQLite não será apagado.\n\nContinuar?",
                "Migrar SQLite → PostgreSQL",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                var result = SqliteToPostgresMigrator.Migrate(truncateFirst: true);
                MessageBox.Show(result.Message, result.Success ? "Migração" : "Erro",
                    MessageBoxButton.OK,
                    result.Success ? MessageBoxImage.Information : MessageBoxImage.Error);
                AtualizarStatusBanco();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnSalvar_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var atual = EmpresaConfigStore.Current;
                var c = new EmpresaConfig
                {
                    Nome = TxtNome.Text.Trim(),
                    Subtitulo = TxtSubtitulo.Text.Trim(),
                    RazaoSocial = TxtRazao.Text.Trim(),
                    NomeFantasia = TxtFantasia.Text.Trim(),
                    Cnpj = TxtCnpj.Text.Trim(),
                    Ie = TxtIe.Text.Trim(),
                    Im = TxtIm.Text.Trim(),
                    Cnae = TxtCnae.Text.Trim(),
                    Telefone = TxtTelefone.Text.Trim(),
                    Email = TxtEmail.Text.Trim(),
                    Endereco = TxtEndereco.Text.Trim(),
                    Numero = TxtNumero.Text.Trim(),
                    Complemento = TxtComplemento.Text.Trim(),
                    Bairro = TxtBairro.Text.Trim(),
                    Cidade = TxtCidade.Text.Trim(),
                    Uf = TxtUf.Text.Trim().ToUpperInvariant(),
                    Cep = TxtCep.Text.Trim(),
                    CodigoIbge = TxtIbge.Text.Trim(),
                    RegimeTributario = (CbRegime.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "1",
                    AmbienteNfe = (CbAmbiente.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "2",
                    SerieNfe = TxtSerieNfe.Text.Trim(),
                    UltimoNumeroNfe = TxtUltimoNfe.Text.Trim(),
                    CscIdHomologacao = TxtCscIdHomolog.Text.Trim(),
                    CscTokenHomologacao = TxtCscTokenHomolog.Text.Trim(),
                    CscIdProducao = TxtCscIdProducao.Text.Trim(),
                    CscTokenProducao = TxtCscTokenProducao.Text.Trim(),
                    FiscalApiUrlNfe = TxtFiscalUrlNfe.Text.Trim().TrimEnd('/'),
                    FiscalApiUrlNfce = TxtFiscalUrlNfce.Text.Trim().TrimEnd('/'),
                    FiscalApiKey = TxtFiscalApiKey.Text.Trim(),
                    CertificadoPath = atual.CertificadoPath,
                    LogoPath = TxtLogoPath.Text.Trim(),
                    CupomTitulo = TxtCupomTitulo.Text.Trim(),
                    CupomRodape = TxtCupomRodape.Text.Trim(),
                    IbsCbsPreset = (CbIbsCbsPreset.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "teste2026",
                    IbsCbsCalculoAutomatico = ChkIbsCbsAuto.IsChecked == true,
                    IbsCbsDestaqueObrigatorio = ChkIbsCbsDestaque.IsChecked == true,
                    CbsAliquota = MoneyInputHelper.Parse(TxtCbsAliq.Text),
                    IbsAliquota = MoneyInputHelper.Parse(TxtIbsAliq.Text),
                    IbsAliquotaUf = MoneyInputHelper.Parse(TxtIbsUfAliq.Text),
                    IbsAliquotaMun = MoneyInputHelper.Parse(TxtIbsMunAliq.Text)
                };

                EmpresaConfigStore.Save(c);

                if (CbImpressora.SelectedItem is string printer && !printer.StartsWith("("))
                    DeviceSettingsStore.SetPrinter(printer);
                if (CbScanner.SelectedItem is string scanner && !scanner.StartsWith("("))
                    DeviceSettingsStore.SetScanner(scanner);

                MessageBox.Show("Configurações salvas com sucesso!", "Configurações",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao salvar: {ex.Message}", "Configurações",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnTestarFiscalNfe_Click(object sender, RoutedEventArgs e) =>
            await TestarConexaoFiscalAsync(TxtFiscalUrlNfe.Text.Trim(), "NF-e");

        private async void BtnTestarFiscalNfce_Click(object sender, RoutedEventArgs e) =>
            await TestarConexaoFiscalAsync(TxtFiscalUrlNfce.Text.Trim(), "NFC-e");

        private async System.Threading.Tasks.Task TestarConexaoFiscalAsync(string baseUrl, string rotulo)
        {
            LblFiscalApiStatus.Text = $"⏳ Testando conexão com a API {rotulo}...";
            LblFiscalApiStatus.Foreground = System.Windows.Media.Brushes.Gray;

            var resultado = await FiscalApiClient.TestarConexaoAsync(baseUrl);
            if (resultado.Sucesso)
            {
                LblFiscalApiStatus.Text = $"✅ API {rotulo} respondendo normalmente ({baseUrl}).";
                LblFiscalApiStatus.Foreground = System.Windows.Media.Brushes.SeaGreen;
            }
            else
            {
                LblFiscalApiStatus.Text = $"❌ Falha ao conectar na API {rotulo}: {resultado.ResumoErro()}";
                LblFiscalApiStatus.Foreground = System.Windows.Media.Brushes.IndianRed;
            }
        }

        private void CbIbsCbsPreset_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;
            string? tag = (CbIbsCbsPreset.SelectedItem as ComboBoxItem)?.Tag?.ToString();
            if (tag == "personalizado") return;
            var (cbs, ibs, uf, mun) = ReformaTributariaService.AliquotasDoPreset(tag);
            TxtCbsAliq.Text = cbs.ToString("0.####");
            TxtIbsAliq.Text = ibs.ToString("0.####");
            TxtIbsUfAliq.Text = uf.ToString("0.####");
            TxtIbsMunAliq.Text = mun.ToString("0.####");
            AtualizarSimulacaoIbsCbs();
        }

        private void BtnSimularIbsCbs_Click(object sender, RoutedEventArgs e) => AtualizarSimulacaoIbsCbs();

        private void AtualizarSimulacaoIbsCbs()
        {
            if (LblIbsCbsSimulacao == null) return;
            var temp = new EmpresaConfig
            {
                IbsCbsPreset = (CbIbsCbsPreset.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "teste2026",
                CbsAliquota = MoneyInputHelper.Parse(TxtCbsAliq?.Text),
                IbsAliquota = MoneyInputHelper.Parse(TxtIbsAliq?.Text),
                IbsAliquotaUf = MoneyInputHelper.Parse(TxtIbsUfAliq?.Text),
                IbsAliquotaMun = MoneyInputHelper.Parse(TxtIbsMunAliq?.Text),
                RegimeTributario = (CbRegime.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "1"
            };
            var r = ReformaTributariaService.Calcular(1000m, temp);
            LblIbsCbsSimulacao.Text =
                $"CBS: {r.ValorCbs:C2} ({r.AliquotaCbs:0.####}%) · " +
                $"IBS: {r.ValorIbs:C2} ({r.AliquotaIbs:0.####}% — UF {r.ValorIbsUf:C2} / Mun {r.ValorIbsMun:C2}) · " +
                $"Total IVA: {r.ValorTotalIva:C2}\n{r.Observacao}";
        }

        private void BtnLogo_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "Imagens|*.png;*.jpg;*.jpeg;*.bmp;*.ico|Todos|*.*"
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                string destDir = Path.Combine(AppContext.BaseDirectory, "branding");
                Directory.CreateDirectory(destDir);
                string dest = Path.Combine(destDir, "logo" + Path.GetExtension(dlg.FileName));
                File.Copy(dlg.FileName, dest, overwrite: true);
                TxtLogoPath.Text = dest;
                if (TxtLogoPathFiscal != null) TxtLogoPathFiscal.Text = dest;
                MostrarLogo(dest);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao copiar logo: {ex.Message}");
            }
        }

        private void BtnRefreshDevices_Click(object sender, RoutedEventArgs e)
        {
            LoadDevices();
            MessageBox.Show("Lista atualizada.", "Dispositivos", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void LoadDevices()
        {
            var printers = InstalledDevicesService.GetPrinters();
            CbImpressora.ItemsSource = printers;
            if (!string.IsNullOrWhiteSpace(DeviceSettingsStore.Current.SelectedPrinter) &&
                printers.Contains(DeviceSettingsStore.Current.SelectedPrinter))
                CbImpressora.SelectedItem = DeviceSettingsStore.Current.SelectedPrinter;
            else if (printers.Count > 0)
                CbImpressora.SelectedIndex = 0;

            var scanners = InstalledDevicesService.GetScanners();
            CbScanner.ItemsSource = scanners;
            if (!string.IsNullOrWhiteSpace(DeviceSettingsStore.Current.SelectedScanner))
            {
                var match = scanners.FirstOrDefault(s =>
                    s.Equals(DeviceSettingsStore.Current.SelectedScanner, StringComparison.OrdinalIgnoreCase));
                if (match != null) CbScanner.SelectedItem = match;
            }
            else if (scanners.Count > 0)
                CbScanner.SelectedIndex = 0;
        }

        private void MostrarLogo(string? path)
        {
            try
            {
                if (TxtLogoPath != null) TxtLogoPath.Text = path ?? "";
                if (TxtLogoPathFiscal != null) TxtLogoPathFiscal.Text = path ?? "";

                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                {
                    if (ImgLogo != null) ImgLogo.Source = null;
                    if (ImgLogoFiscal != null) ImgLogoFiscal.Source = null;
                    return;
                }
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.UriSource = new Uri(path, UriKind.Absolute);
                bmp.EndInit();
                if (ImgLogo != null) ImgLogo.Source = bmp;
                if (ImgLogoFiscal != null) ImgLogoFiscal.Source = bmp;
            }
            catch
            {
                if (ImgLogo != null) ImgLogo.Source = null;
                if (ImgLogoFiscal != null) ImgLogoFiscal.Source = null;
            }
        }

        // Aba Integrações removida — cadastro de APIs externas não é mais utilizado.
    }
}
