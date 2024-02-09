using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Npgsql;
using System.Text.Json.Serialization;

// sem criticancia
var connString = "Host=postgres-db;Username=root;Password=root;Database=rinha-db;MaxPoolSize=30;MinPoolSize=5;Connection Pruning Interval=1;Connection Idle Lifetime=2;Enlist=false;No Reset On Close=true;Pooling=true";

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddMemoryCache();

var app = builder.Build();

SetarClientesNoCache(app);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapPost("/clientes/{id}/transacoes", async (
    [FromServices] IMemoryCache memoryCache,
    [FromRoute] int id,
    [FromBody] CriarTransacaoRequest transacao) =>
{
    var clienteExiste = memoryCache.TryGetValue(id, out _);
    if (!clienteExiste)
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

    var valorTransacao = transacao.Tipo == "c" ? transacao.Valor.GetInt32() : -transacao.Valor.GetInt32();

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
        await reader.CloseAsync();
        await conn.CloseAsync();
        return Results.UnprocessableEntity();
    }

    var novoSaldo = reader["cliente_novo_saldo"];
    var limite = reader["cliente_limite"];

    await reader.CloseAsync();
    await conn.CloseAsync();
    return Results.Ok(new { limite, saldo = novoSaldo });
});

app.MapGet("/clientes/{id}/extrato", async (
    [FromServices] IMemoryCache memoryCache,
    [FromRoute] int id) =>
{
    var clienteExiste = memoryCache.TryGetValue(id, out _);
    if (!clienteExiste)
    {
        return Results.NotFound();
    }

    using var conn = new NpgsqlConnection(connString);
    await conn.OpenAsync();

    await using var buscarSaldoClienteCommand = new NpgsqlCommand(
        "select saldo as total, now() as data_extrato, limite from clientes where id = @ClienteId",
        conn);
    buscarSaldoClienteCommand.Parameters.AddWithValue("ClienteId", id);

    await using var readerSaldoCliente = await buscarSaldoClienteCommand.ExecuteReaderAsync();
    var existeSaldoCliente = await readerSaldoCliente.ReadAsync();
    var saldoCliente = new ExtratoSaldoDto
    {
        Total = readerSaldoCliente.GetInt32(0),
        DataExtrato = readerSaldoCliente.GetDateTime(1),
        Limite = readerSaldoCliente.GetInt32(2)
    };
    await readerSaldoCliente.CloseAsync();

    await using var buscarTransacoesCommand = new NpgsqlCommand(
        "select valor, tipo, descricao, realizada_em from transacoes where cliente_id = @ClienteId order by realizada_em desc limit 10",
        conn);
    buscarTransacoesCommand.Parameters.AddWithValue("ClienteId", id);
    await using var buscarTransacoesReader = await buscarTransacoesCommand.ExecuteReaderAsync();
    var transacoes = new List<TransacaoDto>();
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
    await conn.CloseAsync();

    var resultado = new ExtratoDto
    {
        Saldo = saldoCliente,
        UltimasTransacoes = transacoes
    };
    return Results.Ok(resultado);
});

app.Run();

async void SetarClientesNoCache(IHost app)
{
    // fiquei com peso na consciencia de verificar se o cliente existe apenas verificando se o ID é maior que 5.
    // entao pelo menos coloquei um cache do brabo
    // hihi

    var memoryCache = app.Services.GetService<IMemoryCache>()!;

    var conn = new NpgsqlConnection(connString);
    conn.Open();
    var buscarClientesCommand = new NpgsqlCommand("SELECT id FROM clientes", conn);
    var reader = buscarClientesCommand.ExecuteReader();
    while (reader.Read())
    {
        var id = reader.GetInt32(0);
        memoryCache.Set(id, id, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
        });
    }
    reader.Close();
    conn.Close();

    Console.WriteLine("clientes foram salvos no cache :)");
}

public record ExtratoDto
{
    [JsonPropertyName("saldo")]
    public ExtratoSaldoDto Saldo { get; set; }
    [JsonPropertyName("ultimas_transacoes")]
    public IList<TransacaoDto> UltimasTransacoes { get; set; }
}

public record ExtratoSaldoDto
{
    [JsonPropertyName("total")]
    public int Total { get; set; }
    [JsonPropertyName("data_extrato")]
    public DateTime DataExtrato { get; set; }
    [JsonPropertyName("limite")]
    public int Limite { get; set; }
}

public record TransacaoDto
{
    [JsonPropertyName("valor")]
    public int Valor { get; set; }
    [JsonPropertyName("tipo")]
    public string Tipo { get; set; }
    [JsonPropertyName("descricao")]
    public string Descricao { get; set; }
    [JsonPropertyName("realizada_em")]
    public DateTime RealizadaEm { get; set; }
}
