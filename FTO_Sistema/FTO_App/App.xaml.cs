using System;
using System.Windows;
using FTO_App.Services;

namespace FTO_App
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            try
            {
                EmpresaConfigStore.Load();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Configuração (.env)",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Shutdown();
                return;
            }

            DeviceSettingsStore.Load();
            base.OnStartup(e);
        }
    }
}
