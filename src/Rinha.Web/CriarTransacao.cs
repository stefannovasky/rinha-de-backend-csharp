using System.Text.Json;

public record CriarTransacao
{
    public JsonElement Valor { get; set; }
    public string Tipo { get; set; }
    public string Descricao { get; set; }

    public bool EhValido()
    {
        if (Valor.ValueKind == JsonValueKind.Null || Tipo == null || Descricao == null)
        {
            return false;
        }

        var valorEhInteiro = Valor.TryGetInt32(out int valor);
        if (!valorEhInteiro)
        {
            return false;
        }

        var valorEhNegativo = valor < 1;
        if (valorEhNegativo)
        {
            return false;
        }

        var tipoEhValido = Tipo == "c" || Tipo == "d";
        if (!tipoEhValido)
        {
            return false;
        }

        var descricaoEhValida = Descricao.Length > 0 && Descricao.Length < 11;
        if (!descricaoEhValida)
        {
            return false;
        }

        return true;
    }
}
