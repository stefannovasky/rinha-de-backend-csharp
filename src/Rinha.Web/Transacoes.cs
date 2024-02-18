using Npgsql;

namespace Rinha.Web;

public static class Transacoes
{
    public static async Task<SaldoResponse> BuscarSaldoCliente(NpgsqlConnection conn, int idCliente)
    {
        await using var buscarSaldoClienteCommand = new NpgsqlCommand(
            "SELECT saldo as total, now() as data_extrato FROM clientes WHERE id = @ClienteId",
            conn);
        buscarSaldoClienteCommand.Parameters.AddWithValue("ClienteId", idCliente);

        await using var readerSaldoCliente = await buscarSaldoClienteCommand.ExecuteReaderAsync();
        await readerSaldoCliente.ReadAsync();
        var saldoCliente = new SaldoResponse
        {
            Total = readerSaldoCliente.GetInt32(0),
            DataExtrato = readerSaldoCliente.GetDateTime(1)
        };
        return saldoCliente;
    }

    public static async Task<IList<TransacaoResponse>> BuscarUltimasTransacoesCliente(NpgsqlConnection conn, int idCliente)
    {
        var transacoes = new List<TransacaoResponse>();

        await using var buscarTransacoesCommand = new NpgsqlCommand(
            "SELECT valor, tipo, descricao, realizada_em FROM transacoes WHERE cliente_id = @ClienteId ORDER BY realizada_em DESC LIMIT 10",
            conn);
        buscarTransacoesCommand.Parameters.AddWithValue("ClienteId", idCliente);

        await using var buscarTransacoesReader = await buscarTransacoesCommand.ExecuteReaderAsync();
        while (await buscarTransacoesReader.ReadAsync())
        {
            transacoes.Add(new TransacaoResponse
            {
                Valor = buscarTransacoesReader.GetInt32(0),
                Tipo = buscarTransacoesReader.GetString(1),
                Descricao = buscarTransacoesReader.GetString(2),
                RealizadaEm = buscarTransacoesReader.GetDateTime(3),
            });
        }

        return transacoes;
    }

    public static async Task<Result<int>> CriarTransacao(
        NpgsqlConnection conn,
        int clienteId,
        Transacao transacao)
    {
        var nomeFuncaoSql = transacao.Tipo == "c" ? "credito" : "debito";
        var sql = $"SELECT * FROM {nomeFuncaoSql}(@ClienteId, @ValorTransacao, @DescricaoTransacao);";
        await using var command = new NpgsqlCommand(sql, conn);
        command.Parameters.AddWithValue("ClienteId", clienteId);
        command.Parameters.AddWithValue("ValorTransacao", transacao.Valor);
        command.Parameters.AddWithValue("DescricaoTransacao", transacao.Descricao);

        await using var criarTransacaoReader = await command.ExecuteReaderAsync();
        var sucesso = await criarTransacaoReader.ReadAsync();
        if (!sucesso)
        {
            return new(false, 0);
        }
        var novoSaldo = criarTransacaoReader.GetInt32(0);
        return new(true, novoSaldo);
    }
}
