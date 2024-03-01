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

    public static async Task<List<ClienteLimite>> BuscarLimiteClientes(NpgsqlConnection conn)
    {
        var resultado = new List<ClienteLimite>();

        await using var command = new NpgsqlCommand("SELECT id, limite FROM clientes", conn);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            resultado.Add(new(reader.GetInt32(0), reader.GetInt32(1)));
        }

        return resultado;
    }
}
