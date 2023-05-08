using ServiceStack.OrmLite;
using System.Text.Json.Serialization;
using UnifiedStockExchange.Services;

var builder = WebApplication.CreateBuilder(args);
AddServices(builder);

var app = builder.Build();
ConfigureServices(app);
app.Run();

void AddServices(WebApplicationBuilder builder)
{
    IConfiguration config = builder.Configuration;

    builder.Services.AddControllers()
        .AddJsonOptions(options =>
            options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter())
        );
    // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    string connectionString = config.GetConnectionString("DefaultConnection");
    builder.Services.AddSingleton(new OrmLiteConnectionFactory(connectionString, SqliteDialect.Provider));
    
    InitializeServices(builder);
}

static void ConfigureServices(WebApplication app)
{
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(config =>
        {
            config.SwaggerEndpoint("/swagger/v1/swagger.json", "UnifiedStockExchange API");
        });
    }

    // app.UseHttpsRedirection();

    //app.UseAuthorization();
    app.UseWebSockets();

    app.MapControllers();
}

static void InitializeServices(WebApplicationBuilder builder)
{
    builder.Services.AddSingleton<PricePersistenceService>();
    builder.Services.AddSingleton<PriceExchangeService>();
}