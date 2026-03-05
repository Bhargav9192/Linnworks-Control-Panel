using Elasticsearch.Net;
using Serilog;
using Serilog.Sinks.Elasticsearch;
using Serilog.Debugging;
using System;

public static class LoggingConfig
{
    public static void Configure()
    {
        // Allow overriding the Elasticsearch URL from environment (useful when hosted)
        var elasticUrl = Environment.GetEnvironmentVariable("ELASTIC_URL") ?? "http://77.68.17.136:9200";

        // Enable Serilog internal/self logging so failures from sinks are visible in host logs
        SelfLog.Enable(msg => {
            try
            {
                // Write to console so platform log collectors (e.g. Railway) will capture it
                Console.Error.WriteLine("Serilog-SelfLog: " + msg);
            }
            catch
            {
                // swallow - best effort only
            }
        });

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", "LinnworksSimulator")
            .WriteTo.Elasticsearch(new ElasticsearchSinkOptions(new Uri(elasticUrl))
            {
                AutoRegisterTemplate = true,
                IndexFormat = "linnworks-simulator-logs-{0:yyyy.MM.dd}",
                // When emitting fails, write failures to Serilog SelfLog so they appear in host logs
                EmitEventFailure = EmitEventFailureHandling.WriteToSelfLog,
                FailureCallback = (logEvent, ex) => SelfLog.WriteLine($"Elasticsearch sink failure: {ex?.Message}")
            })
            .CreateLogger();
    }
}