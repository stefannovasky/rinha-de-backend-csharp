using System.Text.Json;
using System.Text.Json.Serialization;

namespace Rinha.Web;

public record Result<T>(bool Success, T Value);

public record CriarTransacaoRequest
{
    public JsonElement Valor { get; set; }
    public string Tipo { get; set; }
    public string Descricao { get; set; }

    public Result<TransacaoValidada?> Validar()
    {
        if (Valor.ValueKind != JsonValueKind.Number || Tipo == null || Descricao == null)
        {
            return new(false, null);
        }

        var valorEhInteiro = Valor.TryGetInt32(out int valor);
        if (!valorEhInteiro)
        {
            return new(false, null);
        }

        var valorEhNegativo = valor < 1;
        if (valorEhNegativo)
        {
            return new(false, null);
        }

        var tipoEhValido = Tipo == "c" || Tipo == "d";
        if (!tipoEhValido)
        {
            return new(false, null);
        }

        var descricaoEhValida = Descricao.Length > 0 && Descricao.Length < 11;
        if (!descricaoEhValida)
        {
            return new(false, null);
        }

        return new(true, new TransacaoValidada(valor, Tipo, Descricao));
    }
}

public record CriarTransacaoResponse
{
    [JsonPropertyName("limite")]
    public int Limite { get; set; }
    [JsonPropertyName("saldo")]
    public int Saldo { get; set; }
}

public record TransacaoValidada(int Valor, string Tipo, string Descricao);


public record BuscarExtratoResponse
{
    [JsonPropertyName("saldo")]
    public SaldoDto Saldo { get; set; }
    [JsonPropertyName("ultimas_transacoes")]
    public IList<TransacaoDto> UltimasTransacoes { get; set; }
}

public record SaldoDto
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
