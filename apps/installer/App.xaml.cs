using System.Windows;

namespace Atlas.Installer;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var executableName = Path.GetFileNameWithoutExtension(Environment.ProcessPath ?? string.Empty);
        if (ShouldUninstall(e.Args, executableName))
        {
            try
            {
                BundleInstaller.Uninstall();
                MessageBox.Show("ATLAS CAD removido. Os dados locais da licença foram preservados.", "ATLAS CAD");
            }
            catch (Exception exception)
            {
                MessageBox.Show(exception.Message, "Não foi possível desinstalar", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            Shutdown();
            return;
        }
        new MainWindow().Show();
    }

    internal static bool ShouldUninstall(IEnumerable<string> arguments, string executableName) =>
        arguments.Any(value => value.Equals("--uninstall", StringComparison.OrdinalIgnoreCase)) ||
        executableName.StartsWith("Desinstalar-", StringComparison.OrdinalIgnoreCase) ||
        executableName.StartsWith("Uninstall-", StringComparison.OrdinalIgnoreCase);
}
