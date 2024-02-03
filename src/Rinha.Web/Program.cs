using Npgsql;

var connString = "Host=postgres-db;Username=root;Password=root;Database=rinha-db";

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapGet("/", async (ILogger<Program> logger) =>
{
    await using var dataSource = NpgsqlDataSource.Create(connString);
    await using var connection = await dataSource.OpenConnectionAsync();

    await using var command = new NpgsqlCommand("SELECT 1;", connection);
    await using var reader = await command.ExecuteReaderAsync();

    string result = "";
    while (await reader.ReadAsync())
    {
        result = reader.GetInt32(0).ToString();
    }

    logger.LogInformation("received request");
    return new { Result = result };
})
.WithName("GetWeatherForecast")
.WithOpenApi();

app.Run();
