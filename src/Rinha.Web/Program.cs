using Microsoft.AspNetCore.Mvc;
using Npgsql;

// sem criticancia
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

app.MapPost("/clientes/{id}/transacoes", async (
    [FromRoute] int id,
    [FromBody] CriarTransacao transacao) =>
{
    if (!transacao.EhValido())
    {
        return Results.StatusCode(422);
    }

    await using var dataSource = NpgsqlDataSource.Create(connString);
    await using var conn = await dataSource.OpenConnectionAsync();

    await using var command = new NpgsqlCommand("select 1 from clientes c where c.id = @Id", conn);
    command.Parameters.AddWithValue("Id", id);
    await using var reader = await command.ExecuteReaderAsync();

    var clienteExiste = await reader.ReadAsync();
    if (!clienteExiste)
    {
        return Results.NotFound();
    }

    return Results.Ok(new { id = id });
});

app.Run();
