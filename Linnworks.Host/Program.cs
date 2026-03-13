using LinnworksMacro;
using LinnworksMacro.LinnworksTest;
using LinnworksMacro.Orders;
using Serilog;

LoggingConfig.Configure();
Log.Information("APPLICATION_STARTED");
try
{
    using var client = new HttpClient();
    var response = client.GetAsync("http://77.68.17.136:9200").Result;

    Log.Information("ELASTIC_TEST_SUCCESS StatusCode {StatusCode}", response.StatusCode);
}
catch (Exception ex)
{
    Log.Error("ELASTIC_TEST_FAILED {Error}", ex.Message);
}
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddAWSLambdaHosting(LambdaEventSource.HttpApi);
builder.Host.UseSerilog();
// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.WebHost.UseUrls("https://0.0.0.0:7112");
// HttpContextAccessor add karo jethi headers read kari shakay
builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<LinnworksAPI.ApiObjectManager>(provider =>
{
    var config = provider.GetRequiredService<IConfiguration>();
    var httpContext = provider.GetRequiredService<IHttpContextAccessor>().HttpContext;

    var userAccount = httpContext?.Request.Query["userAccount"].ToString();

    if (string.IsNullOrEmpty(userAccount))
    {
        userAccount = httpContext?.Request.Headers["X-User-Account"].ToString();
    }

    if (string.IsNullOrEmpty(userAccount)) userAccount = "Default";
    // 2. AppSettings mathi e user no section lo
    var section = config.GetSection($"Linnworks:{userAccount}");

    // Jo user section na male to Default section try karo
    if (!section.Exists()) section = config.GetSection("Linnworks:Default");

    var appId = Guid.Parse(section["ApplicationId"]);
    var secret = Guid.Parse(section["ApplicationSecret"]);
    var token = Guid.Parse(section["Token"]);
    var userKey = section["UserKey"];

    var authService = new LinnworksAuthService(appId, secret, token, userKey);
    var session = authService.GetValidSessionAsync().GetAwaiter().GetResult();
    var context = new LinnworksAPI.ApiContext(session.Token, session.Server);

    return new LinnworksAPI.ApiObjectManager(context);
});
builder.Services.AddMemoryCache();
builder.Services.AddScoped<CreateOrdersFromSnapshotService>();
builder.Services.AddScoped<Rishvi_CreateOrder_with_Scenarios>();
builder.Services.AddScoped<Rishvi_GetFullStockSnapshot>();
builder.Services.AddScoped<Rishvi_AutoPO_OnOrderProcesing>();
builder.Services.AddScoped<Rishvi_WeighWise_Order_Split_Engine>();
builder.Services.AddScoped<Rishvi_Quantity_Based_Splitting>();
var app = builder.Build();
app.UseSerilogRequestLogging();
// Development only
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseDefaultFiles();   // loads index.html automatically
app.UseStaticFiles();    // enables wwwroot

app.UseAuthorization();

app.MapControllers();
app.Lifetime.ApplicationStopped.Register(() =>
{
    Log.CloseAndFlush();
});
app.Run();