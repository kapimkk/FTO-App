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
                Database.InitTables();
                EmpresaConfigStore.Load();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message +
                    "\n\nConfigure o PostgreSQL no .env (PGHOST/PGDATABASE/PGUSER/PGPASSWORD)\n" +
                    "e crie o banco no pgAdmin. Dados da empresa ficam no banco, não no .env.",
                    "Inicialização",
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
