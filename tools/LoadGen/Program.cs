using LoadGen;

if (args is ["--help"] or ["-h"])
{
    Console.WriteLine(Opcoes.Uso);
    return 0;
}

Opcoes opcoes;
try
{
    opcoes = Opcoes.Interpretar(args);
}
catch (ArgumentException ex)
{
    Console.Error.WriteLine(ex.Message);
    Console.Error.WriteLine();
    Console.Error.WriteLine(Opcoes.Uso);
    return 1;
}

using var parar = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true; // não mata o processo: deixa imprimir o resumo
    parar.Cancel();
};
if (opcoes.Duracao is { } duracao)
    parar.CancelAfter(duracao);

using var http = new HttpClient { BaseAddress = opcoes.Url, Timeout = TimeSpan.FromSeconds(10) };
await new GeradorCarga(opcoes, http).ExecutarAsync(parar.Token);
return 0;
