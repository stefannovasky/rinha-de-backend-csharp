using Microsoft.AspNetCore.Mvc;
using Npgsql;
using Rinha.Web;
using System.Collections.Concurrent;
using System.Text.Json.Serialization;

// id_cliente -> limite_cliente
var cache = new ConcurrentDictionary<int, int>();

var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.AddNpgsqlDataSource(
    Environment.GetEnvironmentVariable("POSTGRESQL_CONNECTION_STRING").ToString()
);

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
    [FromServices] NpgsqlDataSource dataSource,
    [FromRoute] int id,
    [FromBody] CriarTransacaoRequest transacao) =>
{
    if (!cache.TryGetValue(id, out int limiteCliente))
    {
        return Results.NotFound();
    }

    await using var conn = await dataSource.OpenConnectionAsync();

    var (sucessoValidacao, transacaoValidada) = transacao.Validar();
    if (!sucessoValidacao)
    {
        return Results.UnprocessableEntity(422);
    }

    var (sucessoTransacao, novoSaldo) = await Transacoes.CriarTransacao(conn, id, transacaoValidada!);
    if (!sucessoTransacao)
    {
        return Results.UnprocessableEntity();
    }

    return Results.Ok(new CriarTransacaoResponse { Limite = limiteCliente, Saldo = novoSaldo });
});

app.MapGet("/clientes/{id}/extrato", async (
    [FromServices] NpgsqlDataSource dataSource,
    [FromRoute] int id) =>
{
    if (!cache.TryGetValue(id, out int limiteCliente))
    {
        return Results.NotFound();
    }

    await using var conn = await dataSource.OpenConnectionAsync();

    var saldoCliente = await Clientes.BuscarSaldoCliente(conn, id);
    var transacoes = await Transacoes.BuscarUltimasTransacoesCliente(conn, id);

    return Results.Ok(new BuscarExtratoResponse
    {
        Saldo = new SaldoResponse
        {
            Total = saldoCliente,
            DataExtrato = DateTime.UtcNow,
            Limite = limiteCliente
        },
        UltimasTransacoes = transacoes
    });
});

await SetarCache(app);
app.Run();

async Task SetarCache(IHost app)
{
    using var conn = app.Services.GetService<NpgsqlConnection>()!;
    await conn.OpenAsync();

    var clientesLimites = await Clientes.BuscarLimiteClientes(conn);
    clientesLimites.ForEach(cl => cache.TryAdd(cl.Id, cl.Limite));

    await conn.CloseAsync();
}

[JsonSerializable(typeof(CriarTransacaoResponse))]
[JsonSerializable(typeof(CriarTransacaoRequest))]
[JsonSerializable(typeof(BuscarExtratoResponse))]
[JsonSerializable(typeof(SaldoResponse))]
[JsonSerializable(typeof(TransacaoResponse))]
[JsonSerializable(typeof(IList<TransacaoResponse>))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(DateTime))]
internal partial class AppJsonSerializerContext : JsonSerializerContext { }
