using Npgsql;

namespace Rinha.Web;

public static class Transacoes
{
    public static async Task<ExtratoSaldoDto> BuscarSaldoCliente(NpgsqlConnection conn, int idCliente)
    {
        await using var buscarSaldoClienteCommand = new NpgsqlCommand(
            "SELECT saldo as total, now() as data_extrato FROM clientes WHERE id = @ClienteId",
            conn);
        buscarSaldoClienteCommand.Parameters.AddWithValue("ClienteId", idCliente);
        await using var readerSaldoCliente = await buscarSaldoClienteCommand.ExecuteReaderAsync();
        await readerSaldoCliente.ReadAsync();
        var saldoCliente = new ExtratoSaldoDto
        {
            Total = readerSaldoCliente.GetInt32(0),
            DataExtrato = readerSaldoCliente.GetDateTime(1)
        };
        return saldoCliente;
    }

    public static async Task<IList<TransacaoDto>> BuscarUltimasTransacoesCliente(NpgsqlConnection conn, int idCliente)
    {
        var transacoes = new List<TransacaoDto>();
        await using var buscarTransacoesCommand = new NpgsqlCommand(
            "SELECT valor, tipo, descricao, realizada_em FROM transacoes WHERE cliente_id = @ClienteId ORDER BY realizada_em DESC LIMIT 10",
            conn);
        buscarTransacoesCommand.Parameters.AddWithValue("ClienteId", idCliente);
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
        return transacoes;
    }

    public static async Task<(bool Sucesso, int SaldoCliente)> Transacionar(
        NpgsqlConnection conn,
        int clienteId,
        TransacaoValidada transacao)
    {
        var nomeFuncaoSql = transacao.Tipo == "c" ? "credito" : "debito";
        var sql = $"SELECT * FROM {nomeFuncaoSql}(@ClienteId, @ValorTransacao, @DescricaoTransacao);";
        await using var command = new NpgsqlCommand(sql, conn);
        command.Parameters.AddWithValue("ClienteId", clienteId);
        command.Parameters.AddWithValue("ValorTransacao", transacao.Valor);
        command.Parameters.AddWithValue("DescricaoTransacao", transacao.Descricao);

        await using var criarTransacaoReader = await command.ExecuteReaderAsync();
        var transacaoTeveSucesso = await criarTransacaoReader.ReadAsync();
        if (!transacaoTeveSucesso)
        {
            return (false, 0);
        }

        var novoSaldo = criarTransacaoReader.GetInt32(0);
        return (true, novoSaldo);
    }
}
