using System.Net.Http;
using DataSeed.Cli;
using DataSeed.Cli.Commands;
using DataSeed.Engine;
using DataSeed.Providers;
using DataSeed.Schema;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;

var services = new ServiceCollection();

services.AddSingleton<HttpClient>();
services.AddSingleton<ProviderFactory>();
services.AddSingleton<SchemaParser>();
services.AddSingleton<SchemaValidator>();
services.AddSingleton<PlanSerializer>();
services.AddSingleton<RunExecutor>();

services.AddTransient<InitCommand>();
services.AddTransient<ValidateCommand>();
services.AddTransient<PlanCommand>();
services.AddTransient<RunCommand>();

var registrar = new TypeRegistrar(services);
var app = new CommandApp(registrar);

app.Configure(config =>
{
    config.SetApplicationName("dataseed");

    config.AddCommand<InitCommand>("init")
        .WithDescription("Scaffold a domain schema YAML template")
        .WithExample("init", "my-domain");

    config.AddCommand<ValidateCommand>("validate")
        .WithDescription("Validate schema structure without LLM calls")
        .WithExample("validate", "my-domain.yaml");

    config.AddCommand<PlanCommand>("plan")
        .WithDescription("Generate and persist plan file (LLM calls happen here)")
        .WithExample("plan", "my-domain.yaml", "--provider", "claude-code");

    config.AddCommand<RunCommand>("run")
        .WithDescription("Execute persisted plan and write JSON output")
        .WithExample("run", "my-domain.yaml");
});

return app.Run(args);
