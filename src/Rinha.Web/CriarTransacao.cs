public record CriarTransacao
{
    public string Valor { get; set; }
    public string Tipo { get; set; }
    public string Descricao { get; set; }

    public bool EhValido()
    {
        if (Valor == null || Tipo == null || Descricao == null)
        {
            return false;
        }

        var valorEhUmInteiro = int.TryParse(Valor, out int valor);
        if (!valorEhUmInteiro)
        {
            return false;
        }

        var valorEhPositivo = valor > 0;
        if (!valorEhPositivo)
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
