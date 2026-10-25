using System.Diagnostics;
using System.Windows;

namespace Atlas.Installer;

public partial class MainWindow : Window
{
    private readonly IReadOnlyList<AutoCadInstallation> installations;
    private CancellationTokenSource? cancellation;

    public MainWindow()
    {
        InitializeComponent();
        installations = AutoCadDetector.Detect();
        StepTitle.Text = installations.Count == 0 ? "AutoCAD compatível não encontrado" : "Pronto para instalar";
        StatusText.Text = installations.Count == 0
            ? "Instale uma versão compatível do AutoCAD entre 2022 e 2026 e execute este assistente novamente."
            : "As versões abaixo serão configuradas para o usuário atual. Feche o AutoCAD antes de continuar.";
        foreach (var installation in installations) VersionsList.Items.Add("✓ " + installation.Label);
        InstallButton.IsEnabled = installations.Count > 0;
    }

    private async void InstallButton_Click(object sender, RoutedEventArgs e)
    {
        if (Process.GetProcessesByName("acad").Length > 0)
        {
            MessageBox.Show("Salve seus desenhos e feche todas as janelas do AutoCAD antes de continuar.", "AutoCAD aberto", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        cancellation = new CancellationTokenSource();
        InstallButton.IsEnabled = false;
        CloseButton.Content = "Cancelar";
        Progress.Visibility = Visibility.Visible;
        try
        {
            StepTitle.Text = "Instalando componentes";
            StatusText.Text = "Validando e instalando os componentes corretos para cada versão…";
            await Task.Run(() => BundleInstaller.Install(installations), cancellation.Token);

            var settings = InstallerSettings.Load();
            using var license = new DeviceLicenseInstaller(settings);
            StepTitle.Text = "Vincule sua conta";
            StatusText.Text = "Uma página segura será aberta. Entre na sua conta e confirme este computador.";
            var pairing = await license.CreatePairingAsync(installations.Select(value => value.Label).ToArray(), cancellation.Token);
            PairingCode.Text = pairing.UserCode;
            CodePanel.Visibility = Visibility.Visible;
            Process.Start(new ProcessStartInfo(pairing.VerificationUri) { UseShellExecute = true });

            while (true)
            {
                await Task.Delay(TimeSpan.FromSeconds(pairing.PollIntervalSeconds), cancellation.Token);
                var status = await license.PollAsync(pairing.PairingId, cancellation.Token);
                if (status.Status == "approved" || status.Status == "completed") break;
                if (status.Status == "rejected") throw new InstallerException("pairing_rejected", "A vinculação foi recusada no navegador.");
                if (status.Status == "expired") throw new InstallerException("pairing_expired", "A vinculação expirou. Execute o instalador novamente.");
            }

            StepTitle.Text = "Ativando licença";
            StatusText.Text = "Preparando a licença e o catálogo protegido…";
            var catalog = BundleInstaller.FindInstalledCatalog(installations);
            await license.CompleteAsync(pairing.PairingId, catalog, cancellation.Token);
            StepTitle.Text = "ATLAS CAD pronto para uso";
            StatusText.Text = "A instalação, a ativação e o catálogo foram concluídos. Abra o AutoCAD e execute ATLAS_DIAGNOSTICS para conferir.";
            Progress.Visibility = Visibility.Collapsed;
            CodePanel.Visibility = Visibility.Collapsed;
            CloseButton.Content = "Concluir";
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "Operação cancelada. Os componentes já instalados foram preservados e você pode tentar novamente.";
            Progress.Visibility = Visibility.Collapsed;
            InstallButton.IsEnabled = true;
        }
        catch (Exception exception)
        {
            var errorCode = exception is InstallerException installerException
                ? installerException.Code
                : "installer_unexpected_error";
            var errorMessage = exception.Message + Environment.NewLine + Environment.NewLine +
                "Código do erro: " + errorCode;
            StepTitle.Text = "Não foi possível concluir";
            StatusText.Text = errorMessage;
            Progress.Visibility = Visibility.Collapsed;
            CodePanel.Visibility = Visibility.Collapsed;
            InstallButton.IsEnabled = true;
            CloseButton.Content = "Fechar";
            MessageBox.Show(
                this,
                errorMessage,
                "Erro na instalação do ATLAS CAD",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        if (Progress.Visibility == Visibility.Visible)
        {
            cancellation?.Cancel();
            return;
        }
        Close();
    }
}
