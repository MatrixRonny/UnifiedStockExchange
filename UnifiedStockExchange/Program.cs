using ServiceStack.OrmLite;
using System.Data;
using UnifiedStockExchange.Services;

var builder = WebApplication.CreateBuilder(args);
AddServices(builder);

var app = builder.Build();
ConfigureServices(app);
app.Run();

void AddServices(WebApplicationBuilder builder)
{
    IConfiguration config = builder.Configuration;

    builder.Services.AddControllers();
    // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    string connectionString = config.GetConnectionString("DefaultConnection");
    builder.Services.AddSingleton(new OrmLiteConnectionFactory(connectionString, SqliteDialect.Provider));

    builder.Services.AddSingleton<PricePersistenceService>();
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

    app.UseHttpsRedirection();

    app.UseAuthorization();

    app.MapControllers();
}