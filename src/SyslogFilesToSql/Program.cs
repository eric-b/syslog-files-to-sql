using FluentMigrator.Runner;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using OpenTelemetry.Exporter;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using SyslogFilesToSql.Components;
using SyslogFilesToSql.Components.SqlImport;
using SyslogFilesToSql.Components.SqlMigration;
using SyslogFilesToSql.Components.SyslogFiles;
using SyslogFilesToSql.Migrations.Migrations._001;
using SyslogFilesToSql.Npgsql.Datalayer;
using System;
using System.Threading.Tasks;

namespace SyslogFilesToSql
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            try
            {
                using (IHost host = CreateHostBuilder(args).Build())
                {
                    try
                    {
                        await host.RunAsync();
                    }
                    finally
                    {
                        Tracing.OpenTelemetry.ActivitySource.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        static HostApplicationBuilder CreateHostBuilder(string[] args)
        {
            HostApplicationBuilder appBuilder = Host.CreateApplicationBuilder(args);
#if DEBUG
            appBuilder.Configuration.AddUserSecrets<Program>();
#endif

            appBuilder.Logging.AddSimpleConsole(c => c.TimestampFormat = "[yyyy-MM-dd HH:mm:ss]");
            ConfigureOpenTelemetryTracing(appBuilder);

            appBuilder.Services.AddHostedService<SyslogFilesToSqlBgService>();
            appBuilder.Services.AddSingleton<SqlMigrationRunner>();
            appBuilder.Services.AddSingleton<SyslogFilesWatcher>();
            appBuilder.Services.Configure<SyslogFilesWatcherOptions>(appBuilder.Configuration.GetSection("Syslog"));
            appBuilder.Services.AddSingleton<SyslogSqlImporter>();
            appBuilder.Services.Configure<SyslogSqlImporterOptions>(options =>
            {
                appBuilder.Configuration.GetSection("Syslog").Bind(options);
                appBuilder.Configuration.GetSection("Db").Bind(options);
            });
            appBuilder.Services.AddSingleton<Db>();
            appBuilder.Services.Configure<DbOptions>(appBuilder.Configuration.GetSection("Db"));

            appBuilder.Services
                .AddFluentMigratorCore()
                .ConfigureRunner(rb => rb
                    .AddPostgres()
                    .WithGlobalConnectionString(services => services.GetRequiredService<IOptions<DbOptions>>().Value.GetConnectionStringWithPassword())
                    .ScanIn(typeof(Baseline).Assembly).For.Migrations().For.EmbeddedResources());

            return appBuilder;
        }

        private static void ConfigureOpenTelemetryTracing(HostApplicationBuilder appBuilder)
        {
            IConfigurationSection otelTracingConfig = appBuilder.Configuration.GetSection("Tracing:OpenTelemetryExporter");
            Uri? tracingEndpoint = otelTracingConfig.GetValue<Uri>("Endpoint");
            if (tracingEndpoint != null)
            {
                appBuilder.Services.AddOpenTelemetry()
                .ConfigureResource(r => r.AddService(
                    serviceName: "SyslogFilesToSql",
                    serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString(),
                    serviceInstanceId: Environment.MachineName))
                .WithTracing(otelBuilder =>
                {
                    otelBuilder
                        .AddSource(Tracing.OpenTelemetry.ActivitySourceName)
                        .AddNpgsql()
                        .SetSampler(new AlwaysOnSampler())
                        .AddOtlpExporter(opt =>
                        {
                            opt.Endpoint = tracingEndpoint;
                            string? protocol = otelTracingConfig.GetValue<string>("Protocol");
                            if (Enum.TryParse<OtlpExportProtocol>(protocol, out var otelExportProtocol))
                            {
                                opt.Protocol = otelExportProtocol;
                            }
                            Console.WriteLine($"OTLP Exporter is using {opt.Protocol} protocol and endpoint {opt.Endpoint}.");
                        });
                });
            }
        }
    }
}
