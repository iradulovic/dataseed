using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DataSeed.Engine;

namespace DataSeed.Providers;

public class ClaudeCodeCliProvider : ILlmProvider
{
    public async Task<string> CompleteAsync(string prompt, CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo("claude")
        {
            ArgumentList = { "-p", prompt },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start 'claude' process. Ensure Claude Code CLI is installed.");

        var stdout = new StringBuilder();
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) stdout.AppendLine(e.Data); };
        process.BeginOutputReadLine();

        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
        {
            var err = await process.StandardError.ReadToEndAsync(ct);
            throw new InvalidOperationException($"claude CLI exited with code {process.ExitCode}: {err}");
        }

        return stdout.ToString().Trim();
    }
}
