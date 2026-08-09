using System;
using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace FTO_App.Views
{
    /// <summary>Janela simples de visualização/cópia/exportação de um XML de nota fiscal.</summary>
    public partial class XmlViewerWindow : Window
    {
        private readonly string _sugestaoNomeArquivo;

        public XmlViewerWindow(string titulo, string xml, string sugestaoNomeArquivo)
        {
            InitializeComponent();
            LblTitulo.Text = titulo;
            TxtXml.Text = FormatarXmlLegivel(xml);
            _sugestaoNomeArquivo = sugestaoNomeArquivo;
        }

        private static string FormatarXmlLegivel(string xml)
        {
            try
            {
                var doc = System.Xml.Linq.XDocument.Parse(xml);
                return doc.ToString(System.Xml.Linq.SaveOptions.None);
            }
            catch
            {
                return xml;
            }
        }

        private void BtnCopiar_Click(object sender, RoutedEventArgs e)
        {
            try { Clipboard.SetText(TxtXml.Text); }
            catch (Exception ex) { MessageBox.Show($"Erro ao copiar: {ex.Message}"); }
        }

        private void BtnSalvar_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog
            {
                Filter = "XML|*.xml|Todos|*.*",
                FileName = _sugestaoNomeArquivo
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                File.WriteAllText(dlg.FileName, TxtXml.Text, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                MessageBox.Show("XML salvo com sucesso!", "XML", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao salvar: {ex.Message}", "XML", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnFechar_Click(object sender, RoutedEventArgs e) => Close();
    }
}
