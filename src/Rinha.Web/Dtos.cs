using System.Text.Json;
using System.Text.Json.Serialization;

namespace Rinha.Web;

public record Result<T>(bool Success, T Value);

public record CriarTransacaoRequest
{
    private static readonly string[] TIPOS = ["c", "d"];

    public JsonElement Valor { get; set; }
    public string Tipo { get; set; }
    public string Descricao { get; set; }

    public Result<Transacao?> Validar()
    {
        if (Valor.ValueKind == JsonValueKind.Number
            && Valor.TryGetInt32(out int valor)
            && valor > 0
            && TIPOS.Contains(Tipo)
            && !string.IsNullOrWhiteSpace(Descricao)
            && Descricao.Length < 11)
        {
            return new(true, new Transacao(valor, Tipo, Descricao));
        }

        return new(false, null);
    }
}

public record Transacao(int Valor, string Tipo, string Descricao);

public record CriarTransacaoResponse
{
    [JsonPropertyName("limite")]
    public int Limite { get; set; }
    [JsonPropertyName("saldo")]
    public int Saldo { get; set; }
}

public record BuscarExtratoResponse
{
    [JsonPropertyName("saldo")]
    public SaldoResponse Saldo { get; set; }
    [JsonPropertyName("ultimas_transacoes")]
    public IList<TransacaoResponse> UltimasTransacoes { get; set; }
}

public record SaldoResponse
{
    [JsonPropertyName("total")]
    public int Total { get; set; }
    [JsonPropertyName("data_extrato")]
    public DateTime DataExtrato { get; set; }
    [JsonPropertyName("limite")]
    public int Limite { get; set; }
}

public record TransacaoResponse
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

public record ClienteLimite(int Id, int Limite);
