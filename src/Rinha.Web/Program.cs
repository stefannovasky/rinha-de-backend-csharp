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
    // validar corpo da requisicao
    var (sucessoValidacao, transacaoValidada) = transacao.Validar();
    if (!sucessoValidacao)
    {
        return Results.UnprocessableEntity(422);
    }

    using var conn = new NpgsqlConnection(connString);
    await conn.OpenAsync();

    // validar se o usuario existe
    if (!cache.TryGetValue(id, out int limiteCliente))
    {
        return Results.NotFound();
    }

    // fazer a transacao
    var nomeFuncaoSql = transacaoValidada!.Tipo == "c" ? "credito" : "debito";
    var sql = $"select * from {nomeFuncaoSql}(@ClienteId, @ValorTransacao, @DescricaoTransacao);";
    await using var command = new NpgsqlCommand(sql, conn);
    command.Parameters.AddWithValue("ClienteId", id);
    command.Parameters.AddWithValue("ValorTransacao", transacaoValidada.Valor);
    command.Parameters.AddWithValue("DescricaoTransacao", transacaoValidada.Descricao);

    await using var criarTransacaoReader = await command.ExecuteReaderAsync();
    var transacaoTeveSucesso = await criarTransacaoReader.ReadAsync();
    if (!transacaoTeveSucesso)
    {
        return Results.UnprocessableEntity();
    }

    var novoSaldo = criarTransacaoReader.GetInt32(0);

    return Results.Ok(new CriarTransacaoResponse { Limite = limiteCliente, Saldo = novoSaldo });
});

app.MapGet("/clientes/{id}/extrato", async ([FromRoute] int id) =>
{
    using var conn = new NpgsqlConnection(connString);
    await conn.OpenAsync();

    // validar se o usuario existe
    if (!cache.TryGetValue(id, out int limiteCliente))
    {
        return Results.NotFound();
    }

    // buscar saldo e transacoes
    await using var buscarSaldoClienteCommand = new NpgsqlCommand(
        "SELECT saldo as total, now() as data_extrato FROM clientes WHERE id = @ClienteId",
        conn);
    buscarSaldoClienteCommand.Parameters.AddWithValue("ClienteId", id);

    await using var readerSaldoCliente = await buscarSaldoClienteCommand.ExecuteReaderAsync();
    await readerSaldoCliente.ReadAsync();
    var saldoCliente = new ExtratoSaldoDto
    {
        Total = readerSaldoCliente.GetInt32(0),
        DataExtrato = readerSaldoCliente.GetDateTime(1),
        Limite = limiteCliente
    };
    await readerSaldoCliente.CloseAsync();

    var transacoes = new List<TransacaoDto>();
    await using var buscarTransacoesCommand = new NpgsqlCommand(
        "SELECT valor, tipo, descricao, realizada_em FROM transacoes WHERE cliente_id = @ClienteId ORDER BY realizada_em DESC LIMIT 10",
        conn);
    buscarTransacoesCommand.Parameters.AddWithValue("ClienteId", id);
    await using var buscarTransacoesReader = await buscarTransacoesCommand.ExecuteReaderAsync();
    while (await buscarTransacoesReader.ReadAsync())
    {
        transacoes.Add(new TransacaoDto
        {
            Valor = buscarTransacoesReader.GetInt32(0),
            Tipo = buscarTransacoesReader.GetString(1),
            Descricao = buscarTransacoesReader.GetString(2),
            RealizadaEm = buscarTransacoesReader.GetDateTime(3),
        });
    }

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
