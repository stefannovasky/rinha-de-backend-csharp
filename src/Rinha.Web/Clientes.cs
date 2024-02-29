using Npgsql;

namespace Rinha.Web;

public static class Clientes
{
    public static async Task<int> BuscarSaldoCliente(NpgsqlConnection conn, int idCliente)
    {
        await using var buscarSaldoClienteCommand = new NpgsqlCommand(
            "SELECT saldo FROM clientes WHERE id = @ClienteId",
            conn);
        buscarSaldoClienteCommand.Parameters.AddWithValue("ClienteId", idCliente);

        await using var readerSaldoCliente = await buscarSaldoClienteCommand.ExecuteReaderAsync();
        await readerSaldoCliente.ReadAsync();
        return readerSaldoCliente.GetInt32(0);
    }
}
