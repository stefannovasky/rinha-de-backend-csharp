using Microsoft.AspNetCore.Mvc;
using Npgsql;
using Rinha.Web;
using System.Collections.Concurrent;
using System.Text.Json.Serialization;

var connString = Environment.GetEnvironmentVariable("POSTGRESQL_CONNECTION_STRING")!.ToString();

// id_cliente -> limite_cliente
var cache = new ConcurrentDictionary<int, int>(); // provavelmente o cache menos adequado do mundo

var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
});

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
    [FromBody] CriarTransacaoRequest transacao) =>
{
    if (!cache.TryGetValue(id, out int limiteCliente))
    {
        return Results.NotFound();
    }

    var (sucessoValidacao, transacaoValidada) = transacao.Validar();
    if (!sucessoValidacao)
    {
        return Results.UnprocessableEntity(422);
    }

    using var conn = new NpgsqlConnection(connString);
    await conn.OpenAsync();

    var (sucessoTransacao, novoSaldo) = await Transacoes.Transacionar(conn, id, transacaoValidada!);
    if (!sucessoTransacao)
    {
        return Results.UnprocessableEntity();
    }

    return Results.Ok(new CriarTransacaoResponse { Limite = limiteCliente, Saldo = novoSaldo });
});

app.MapGet("/clientes/{id}/extrato", async ([FromRoute] int id) =>
{
    if (!cache.TryGetValue(id, out int limiteCliente))
    {
        return Results.NotFound();
    }

    using var conn = new NpgsqlConnection(connString);
    await conn.OpenAsync();

    var saldoCliente = await Transacoes.BuscarSaldoCliente(conn, id);
    saldoCliente.Limite = limiteCliente;
    var transacoes = await Transacoes.BuscarUltimasTransacoesCliente(conn, id);

    return Results.Ok(new BuscarExtratoResponse
    {
        Saldo = saldoCliente,
        UltimasTransacoes = transacoes
    });
});

SetarCache(app);
app.Run();

void SetarCache(IHost app)
{
    using var conn = new NpgsqlConnection(connString);
    conn.Open();

    using var command = new NpgsqlCommand("SELECT id, limite FROM clientes", conn);
    using var reader = command.ExecuteReader();

    while (reader.Read())
    {
        cache.TryAdd(reader.GetInt32(0), reader.GetInt32(1));
    }

    conn.Close();
}


[JsonSerializable(typeof(BuscarExtratoResponse))]
[JsonSerializable(typeof(CriarTransacaoResponse))]
[JsonSerializable(typeof(CriarTransacaoRequest))]
internal partial class AppJsonSerializerContext : JsonSerializerContext { }
