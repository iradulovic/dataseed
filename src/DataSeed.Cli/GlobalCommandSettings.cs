using System.ComponentModel;
using Spectre.Console.Cli;

namespace DataSeed.Cli;

public class GlobalCommandSettings : CommandSettings
{
    [CommandOption("--provider <PROVIDER>")]
    [Description("LLM provider: claude-code, codex, anthropic, openai")]
    [DefaultValue("claude-code")]
    public string Provider { get; set; } = "claude-code";

    [CommandOption("--api-key <KEY>")]
    [Description("API key for HTTP providers (falls back to env var)")]
    public string? ApiKey { get; set; }

    [CommandOption("--model <MODEL>")]
    [Description("Model name override (optional)")]
    public string? Model { get; set; }

    [CommandOption("--format <FORMAT>")]
    [Description("Output format: text (default) or json")]
    [DefaultValue("text")]
    public string Format { get; set; } = "text";

    [CommandOption("--quiet")]
    [Description("Suppress decorative output; data only to stdout")]
    public bool Quiet { get; set; }
}
