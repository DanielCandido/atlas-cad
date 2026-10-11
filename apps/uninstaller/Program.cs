using System;
using System.Diagnostics;
using System.IO;

internal static class Program
{
    private static int Main()
    {
        Console.Title = "Desinstalador ATLAS CAD";
        Console.WriteLine("ATLAS CAD - Desinstalacao");
        Console.WriteLine();
        Console.WriteLine("Feche todas as janelas do AutoCAD antes de continuar.");
        Console.Write("Deseja remover o ATLAS CAD deste usuario? [S/N]: ");
        var answer = (Console.ReadLine() ?? string.Empty).Trim();
        if (!answer.Equals("S", StringComparison.OrdinalIgnoreCase) &&
            !answer.Equals("SIM", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("Desinstalacao cancelada.");
            return 0;
        }

        var script = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Uninstall-AtlasCad.ps1");
        if (!File.Exists(script))
        {
            Console.Error.WriteLine("O arquivo Uninstall-AtlasCad.ps1 nao foi encontrado ao lado deste executavel.");
            Pause();
            return 2;
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoLogo -NoProfile -ExecutionPolicy Bypass -File \"" + script + "\" -Scope CurrentUser -NonInteractive",
                UseShellExecute = false
            };
            using (var process = Process.Start(startInfo))
            {
                if (process == null) throw new InvalidOperationException("Nao foi possivel iniciar o PowerShell.");
                process.WaitForExit();
                if (process.ExitCode != 0)
                {
                    Console.Error.WriteLine("A desinstalacao nao foi concluida. Codigo: " + process.ExitCode);
                    Pause();
                    return process.ExitCode;
                }
            }

            Console.WriteLine();
            Console.WriteLine("ATLAS CAD removido com sucesso. Os dados da licenca local foram preservados.");
            Pause();
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("Nao foi possivel desinstalar o ATLAS CAD: " + exception.Message);
            Pause();
            return 1;
        }
    }

    private static void Pause()
    {
        Console.WriteLine("Pressione Enter para fechar.");
        Console.ReadLine();
    }
}
