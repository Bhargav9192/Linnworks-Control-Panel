using Elasticsearch.Net;
using Serilog;
using Serilog.Sinks.Elasticsearch;
using System;

public static class LoggingConfig
{
    public static void Configure()
    {
        var elasticUrl = "http://77.68.17.136:9200";

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", "LinnworksSimulator")
            .WriteTo.Elasticsearch(new ElasticsearchSinkOptions(new Uri(elasticUrl))
            {
                AutoRegisterTemplate = true,
                IndexFormat = "linnworks-simulator-logs-{0:yyyy.MM.dd}"
            })
            .CreateLogger();
    }
}