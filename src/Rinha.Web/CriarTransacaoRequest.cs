using System.Security.Cryptography.Xml;
using System.Text.Json;

public record CriarTransacaoRequest
{
    public JsonElement Valor { get; set; }
    public string Tipo { get; set; }
    public string Descricao { get; set; }

    public (bool, TransacaoValidada?) Validar()
    {
        if (Valor.ValueKind == JsonValueKind.Null || Tipo == null || Descricao == null)
        {
            return (false, null);
        }

        var valorEhInteiro = Valor.TryGetInt32(out int valor);
        if (!valorEhInteiro)
        {
            return (false, null);
        }

        var valorEhNegativo = valor < 1;
        if (valorEhNegativo)
        {
            return (false, null);
        }

        var tipoEhValido = Tipo == "c" || Tipo == "d";
        if (!tipoEhValido)
        {
            return (false, null);
        }

        var descricaoEhValida = Descricao.Length > 0 && Descricao.Length < 11;
        if (!descricaoEhValida)
        {
            return (false, null);
        }

        return (true, new TransacaoValidada(valor, Descricao, Tipo));
    }
}

public record TransacaoValidada(int Valor, string Tipo, string Descricao);
