namespace Terminal.Worker.Configuracao;

/// <summary>Interpreta TERMINAIS: "1-50", "7" ou combinações como "1,2,10-12".</summary>
public static class FaixaTerminais
{
    public static IReadOnlyList<int> Interpretar(string texto)
    {
        var ids = new SortedSet<int>();

        foreach (var parte in texto.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var (inicio, fim) = parte.Split('-', StringSplitOptions.TrimEntries) switch
            {
                [var unico] => (Numero(unico), Numero(unico)),
                [var de, var ate] => (Numero(de), Numero(ate)),
                _ => throw Invalido(),
            };
            if (fim < inicio)
                throw Invalido();

            for (var id = inicio; id <= fim; id++)
                ids.Add(id);
        }

        return ids.Count > 0 ? [.. ids] : throw Invalido();

        int Numero(string s) => int.TryParse(s, out var n) && n > 0 ? n : throw Invalido();

        FormatException Invalido() =>
            new($"TERMINAIS inválido: '{texto}'. Exemplos válidos: 1-50, 7, 1,2,10-12.");
    }
}
