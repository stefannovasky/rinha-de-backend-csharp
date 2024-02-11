using Microsoft.AspNetCore.Mvc;
using Npgsql;
using Rinha.Web;
using System.Collections.Concurrent;
using System.Text.Json.Serialization;

var connString = Environment.GetEnvironmentVariable("POSTGRESQL_CONNECTION_STRING")!.ToString();

var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

var cache = new ConcurrentDictionary<int, int>(); // provavelmente o cache menos adequado do mundo 

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
    if (!cache.ContainsKey(id))
    {
        await using var clienteExisteCommand = new NpgsqlCommand("select 1 from clientes where id = @ClienteId", conn);
        clienteExisteCommand.Parameters.AddWithValue("ClienteId", id);

        await using var clienteExisteReader = await clienteExisteCommand.ExecuteReaderAsync();
        if (!await clienteExisteReader.ReadAsync())
        {
            return Results.NotFound();
        }
        await clienteExisteReader.CloseAsync();

        cache.TryAdd(id, id);
    }

    // fazer a transacao 
    var nomeProcedure = transacaoValidada!.Tipo == "c" ? "credito" : "debito";
    var sql = $"select * from {nomeProcedure}(@ClienteId, @ValorTransacao, @DescricaoTransacao);";
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
    var limite = criarTransacaoReader.GetInt32(1);

    return Results.Ok(new CriarTransacaoResponse { Limite = limite, Saldo = novoSaldo });
});

app.MapGet("/clientes/{id}/extrato", async ([FromRoute] int id) =>
{
    using var conn = new NpgsqlConnection(connString);
    await conn.OpenAsync();

    // validar se o usuario existe 
    if (!cache.ContainsKey(id))
    {
        await using var clienteExisteCommand = new NpgsqlCommand("select 1 from clientes where id = @ClienteId", conn);
        clienteExisteCommand.Parameters.AddWithValue("ClienteId", id);

        await using var clienteExisteReader = await clienteExisteCommand.ExecuteReaderAsync();
        if (!await clienteExisteReader.ReadAsync())
        {
            return Results.NotFound();
        }
        await clienteExisteReader.CloseAsync();

        cache.TryAdd(id, id);
    }

    // buscar saldo e transacoes
    await using var buscarSaldoClienteCommand = new NpgsqlCommand(
        "select saldo as total, now() as data_extrato, limite from clientes where id = @ClienteId",
        conn);
    buscarSaldoClienteCommand.Parameters.AddWithValue("ClienteId", id);

    await using var readerSaldoCliente = await buscarSaldoClienteCommand.ExecuteReaderAsync();
    await readerSaldoCliente.ReadAsync();
    var saldoCliente = new ExtratoSaldoDto
    {
        Total = readerSaldoCliente.GetInt32(0),
        DataExtrato = readerSaldoCliente.GetDateTime(1),
        Limite = readerSaldoCliente.GetInt32(2)
    };
    await readerSaldoCliente.CloseAsync();

    var transacoes = new List<TransacaoDto>();
    await using var buscarTransacoesCommand = new NpgsqlCommand(
        "select valor, tipo, descricao, realizada_em from transacoes where cliente_id = @ClienteId order by realizada_em desc limit 10",
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

    var resultado = new BuscarExtratoResponse
    {
        Saldo = saldoCliente,
        UltimasTransacoes = transacoes
    };
    return Results.Ok(resultado);
});

app.Run();

[JsonSerializable(typeof(BuscarExtratoResponse))]
[JsonSerializable(typeof(CriarTransacaoResponse))]
[JsonSerializable(typeof(CriarTransacaoRequest))]
internal partial class AppJsonSerializerContext : JsonSerializerContext { }