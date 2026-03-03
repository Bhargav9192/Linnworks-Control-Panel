
using Elasticsearch.Net;
using Serilog;
using Serilog.Sinks.Elasticsearch;
using System;

public static class LoggingConfig
{
    public static void Configure()
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .Enrich.FromLogContext()
            .WriteTo.Elasticsearch(
                new ElasticsearchSinkOptions(
                    new Uri("https://my-elasticsearch-project-b2da54.es.us-central1.gcp.elastic.cloud:443")
                )
                {
                    AutoRegisterTemplate = false,
                    DetectElasticsearchVersion = false,
                    IndexFormat = "linnworks-logs-{0:yyyy.MM.dd}",
                    ModifyConnectionSettings = x =>
                        x.ApiKeyAuthentication(
                            new ApiKeyAuthenticationCredentials(
                                Environment.GetEnvironmentVariable("ELASTIC_API_KEY")
                            )
                        )
                        .DisablePing()
                        .RequestTimeout(TimeSpan.FromSeconds(30))
                })
            .CreateLogger();
    }
}
