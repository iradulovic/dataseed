using System;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Threading;
using DataSeed.Schema;
using Spectre.Console;
using Spectre.Console.Cli;

namespace DataSeed.Cli.Commands;

public class ValidateSettings : GlobalCommandSettings
{
    [CommandArgument(0, "<schema.yaml>")]
    [Description("Path to the schema YAML file to validate")]
    public string SchemaFile { get; set; } = string.Empty;

    [CommandOption("--catalog-schema <FILE>")]
    [Description("Path to a catalog schema YAML whose entities are treated as available external refs")]
    public string? CatalogSchema { get; set; }
}

public class ValidateCommand : Command<ValidateSettings>
{
    private readonly SchemaParser _parser;
    private readonly SchemaValidator _validator;

    public ValidateCommand(SchemaParser parser, SchemaValidator validator)
    {
        _parser = parser;
        _validator = validator;
    }

    protected override int Execute(CommandContext context, ValidateSettings settings, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(settings.SchemaFile))
        {
            WriteError(settings, $"File not found: {settings.SchemaFile}");
            return 1;
        }

        try
        {
            var schema = _parser.ParseFile(settings.SchemaFile);
            IEnumerable<string>? externalNames = null;
            if (settings.CatalogSchema is not null && File.Exists(settings.CatalogSchema))
            {
                var catalogSchema = _parser.ParseFile(settings.CatalogSchema);
                externalNames = catalogSchema.Entities.ConvertAll(e => e.Name);
            }
            var errors = _validator.Validate(schema, externalNames);

            if (errors.Count == 0)
            {
                if (settings.Format == "json")
                    Console.WriteLine(JsonSerializer.Serialize(new { success = true, message = "Schema is valid." }));
                else if (!settings.Quiet)
                    AnsiConsole.MarkupLine($"[green]✓[/] {settings.SchemaFile} is valid.");
                return 0;
            }

            if (settings.Format == "json")
            {
                Console.WriteLine(JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "Schema validation failed",
                    details = errors.ConvertAll(e => e.Message)
                }));
            }
            else
            {
                AnsiConsole.MarkupLine($"[red]Schema validation failed:[/] {errors.Count} error(s)");
                foreach (var err in errors)
                    AnsiConsole.MarkupLine($"  [red]•[/] {err.Message}");
            }
            return 1;
        }
        catch (Exception ex)
        {
            WriteError(settings, ex.Message);
            return 1;
        }
    }

    private static void WriteError(ValidateSettings settings, string message)
    {
        if (settings.Format == "json")
            Console.WriteLine(JsonSerializer.Serialize(new { success = false, error = message }));
        else
            AnsiConsole.MarkupLine($"[red]Error:[/] {message}");
    }
}
