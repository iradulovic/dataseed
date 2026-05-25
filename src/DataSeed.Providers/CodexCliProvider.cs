using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DataSeed.Engine;

namespace DataSeed.Providers;

public class CodexCliProvider : ILlmProvider
{
    public async Task<string> CompleteAsync(string prompt, CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo("codex")
        {
            ArgumentList = { prompt },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start 'codex' process. Ensure Codex CLI is installed.");

        var stdout = new StringBuilder();
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) stdout.AppendLine(e.Data); };
        process.BeginOutputReadLine();

        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
        {
            var err = await process.StandardError.ReadToEndAsync(ct);
            throw new InvalidOperationException($"codex CLI exited with code {process.ExitCode}: {err}");
        }

        return stdout.ToString().Trim();
    }
}
