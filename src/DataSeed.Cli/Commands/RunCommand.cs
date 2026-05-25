using System;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Threading;
using DataSeed.Engine;
using DataSeed.Schema;
using Spectre.Console;
using Spectre.Console.Cli;

namespace DataSeed.Cli.Commands;

public class RunSettings : GlobalCommandSettings
{
    [CommandArgument(0, "<schema.yaml>")]
    [Description("Path to the schema YAML file")]
    public string SchemaFile { get; set; } = string.Empty;

    [CommandOption("--catalog-plan <FILE>")]
    [Description("Path to a catalog plan YAML to inject shared reference entities")]
    public string? CatalogPlan { get; set; }

    [CommandOption("--compact")]
    [Description("Minified JSON output (default is pretty-printed)")]
    public bool Compact { get; set; }

    [CommandOption("--seed <SEED>")]
    [Description("Override random seed for reproducible output")]
    public int? Seed { get; set; }
}

public class RunCommand : Command<RunSettings>
{
    private readonly SchemaParser _parser;
    private readonly PlanSerializer _planSerializer;
    private readonly RunExecutor _executor;

    public RunCommand(SchemaParser parser, PlanSerializer planSerializer, RunExecutor executor)
    {
        _parser = parser;
        _planSerializer = planSerializer;
        _executor = executor;
    }

    protected override int Execute(CommandContext context, RunSettings settings, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(settings.SchemaFile))
        {
            WriteError(settings, $"File not found: {settings.SchemaFile}");
            return 1;
        }

        var planFilePath = Path.ChangeExtension(settings.SchemaFile, ".plan.yaml");
        if (!File.Exists(planFilePath))
        {
            WriteError(settings, $"Plan file not found: {planFilePath}. Run 'dataseed plan' first.");
            return 1;
        }

        try
        {
            var schema = _parser.ParseFile(settings.SchemaFile);
            var plan = _planSerializer.ReadFromFile(planFilePath);

            DataSeed.Engine.Models.PlanFile? catalogPlan = null;
            if (settings.CatalogPlan is not null)
            {
                if (!File.Exists(settings.CatalogPlan))
                {
                    WriteError(settings, $"Catalog plan file not found: {settings.CatalogPlan}");
                    return 1;
                }
                catalogPlan = _planSerializer.ReadFromFile(settings.CatalogPlan);
            }

            if (!settings.Quiet)
                AnsiConsole.MarkupLine($"[blue]Generating[/] {schema.Domain}...");

            var outputFolder = _executor.Execute(
                schema,
                plan,
                outputBase: Directory.GetCurrentDirectory(),
                compact: settings.Compact,
                catalogPlan: catalogPlan,
                seedOverride: settings.Seed);

            if (settings.Format == "json")
                Console.WriteLine(JsonSerializer.Serialize(new { success = true, outputFolder }));
            else
            {
                if (!settings.Quiet)
                    AnsiConsole.MarkupLine($"[green]✓[/] Output written to [bold]{outputFolder}[/]");
                Console.WriteLine(outputFolder);
            }

            return 0;
        }
        catch (Exception ex)
        {
            WriteError(settings, ex.Message);
            return 3;
        }
    }

    private static void WriteError(RunSettings settings, string message)
    {
        if (settings.Format == "json")
            Console.WriteLine(JsonSerializer.Serialize(new { success = false, error = message }));
        else
            Console.Error.WriteLine($"Error: {message}");
    }
}
