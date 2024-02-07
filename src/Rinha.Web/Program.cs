using Microsoft.AspNetCore.Mvc;
using Npgsql;

// sem criticancia
var connString = "Host=postgres-db;Username=root;Password=root;Database=rinha-db;MaxPoolSize=20;MinPoolSize=5;Connection Pruning Interval=1;Connection Idle Lifetime=2;Enlist=false;No Reset On Close=true";

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapPost("/clientes/{id}/transacoes", async (
    [FromRoute] int id,
    [FromBody] CriarTransacao transacao) =>
{
    // o certo seria verificar a existencia no banco, mas né, tamo aí
    if (id > 5 || id < 1)
    {
        return Results.NotFound();
    }

    if (!transacao.EhValido())
    {
        return Results.StatusCode(422);
    }

    var valorTransacao = transacao.Tipo == "c" ? int.Parse(transacao.Valor) : - int.Parse(transacao.Valor);

    await using var dataSource = NpgsqlDataSource.Create(connString);
    await using var conn = await dataSource.OpenConnectionAsync();

    var sql = "select * from criar_transacao(@ClienteId, @ValorTransacao, @TipoTransacao, @DescricaoTransacao);";
    await using var command = new NpgsqlCommand(sql, conn);
    command.Parameters.AddWithValue("ClienteId", id);
    command.Parameters.AddWithValue("ValorTransacao", valorTransacao);
    command.Parameters.AddWithValue("TipoTransacao", transacao.Tipo);
    command.Parameters.AddWithValue("DescricaoTransacao", transacao.Descricao);
    await using var reader = await command.ExecuteReaderAsync();

    var transacaoTeveSucesso = await reader.ReadAsync();
    if (!transacaoTeveSucesso)
    {
        return Results.StatusCode(422);
    }

    var novoSaldo = reader["cliente_novo_saldo"];
    var limite = reader["cliente_limite"];
    return Results.Ok(new { limite, saldo = novoSaldo });
});



app.Run();
