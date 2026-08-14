using System;
using System.Windows;
using FTO_App.Services;

namespace FTO_App
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // Sem isto, qualquer falha não tratada em um clique (ex.: queda do PostgreSQL no meio
            // de um DELETE) fecha o sistema na cara do usuário e perde o que estava na tela.
            DispatcherUnhandledException += (_, args) =>
            {
                MessageBox.Show(
                    $"Ocorreu um erro inesperado:\n\n{args.Exception.Message}\n\n" +
                    "A operação foi cancelada, mas o sistema continua aberto.",
                    "FTO", MessageBoxButton.OK, MessageBoxImage.Error);
                args.Handled = true;
            };

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
