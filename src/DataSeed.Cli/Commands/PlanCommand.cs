using System;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DataSeed.Engine;
using DataSeed.Providers;
using DataSeed.Schema;
using Spectre.Console;
using Spectre.Console.Cli;

namespace DataSeed.Cli.Commands;

public class PlanSettings : GlobalCommandSettings
{
    [CommandArgument(0, "<schema.yaml>")]
    [Description("Path to the schema YAML file")]
    public string SchemaFile { get; set; } = string.Empty;

    [CommandOption("--force")]
    [Description("Overwrite existing plan file without prompting")]
    public bool Force { get; set; }

    [CommandOption("--catalog-plan <FILE>")]
    [Description("Path to a catalog plan YAML to inject shared reference entities")]
    public string? CatalogPlan { get; set; }
}

public class PlanCommand : AsyncCommand<PlanSettings>
{
    private readonly SchemaParser _parser;
    private readonly SchemaValidator _validator;
    private readonly ProviderFactory _providerFactory;
    private readonly PlanSerializer _planSerializer;

    public PlanCommand(
        SchemaParser parser,
        SchemaValidator validator,
        ProviderFactory providerFactory,
        PlanSerializer planSerializer)
    {
        _parser = parser;
        _validator = validator;
        _providerFactory = providerFactory;
        _planSerializer = planSerializer;
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, PlanSettings settings, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(settings.SchemaFile))
        {
            WriteError(settings, $"File not found: {settings.SchemaFile}");
            return 1;
        }

        var planFile = Path.ChangeExtension(settings.SchemaFile, ".plan.yaml");

        if (File.Exists(planFile) && !settings.Force && !settings.Quiet)
        {
            if (!AnsiConsole.Confirm($"Plan file '{planFile}' already exists. Overwrite?"))
                return 0;
        }

        try
        {
            var schema = _parser.ParseFile(settings.SchemaFile);
            var errors = _validator.Validate(schema);
            if (errors.Count > 0)
            {
                WriteError(settings, $"Schema validation failed: {errors[0].Message}");
                return 1;
            }

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
                AnsiConsole.MarkupLine($"[blue]Planning[/] {schema.Domain} with provider [yellow]{settings.Provider}[/]...");

            var provider = _providerFactory.Create(settings.Provider, settings.ApiKey, settings.Model);
            var generator = new PlanGenerator(provider);
            var plan = await generator.GenerateAsync(
                schema,
                settings.SchemaFile,
                settings.Provider,
                catalogPlan,
                cancellationToken);

            _planSerializer.WriteToFile(plan, planFile);

            if (settings.Format == "json")
                Console.WriteLine(JsonSerializer.Serialize(new { success = true, planFile }));
            else if (!settings.Quiet)
                AnsiConsole.MarkupLine($"[green]✓[/] Plan written to {planFile}");

            return 0;
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("provider") || ex.Message.Contains("API"))
        {
            WriteError(settings, ex.Message);
            return 2;
        }
        catch (Exception ex)
        {
            WriteError(settings, ex.Message);
            return 3;
        }
    }

    private static void WriteError(PlanSettings settings, string message)
    {
        if (settings.Format == "json")
            Console.WriteLine(JsonSerializer.Serialize(new { success = false, error = message }));
        else
            Console.Error.WriteLine($"Error: {message}");
    }
}
