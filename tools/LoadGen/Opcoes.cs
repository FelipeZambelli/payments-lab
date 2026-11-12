namespace LoadGen;

public sealed record Opcoes(int Tps, int Terminais, TimeSpan? Duracao, Uri Url, int Concorrencia)
{
    public const string Uso = """
        Uso: dotnet run --project tools/LoadGen -- [opções]

          --tps <n>            transações por segundo                      (padrão 10)
          --terminais <m>      sorteia o terminalId entre 1 e m            (padrão 50)
          --duracao <seg>      para depois de <seg> segundos               (padrão: até Ctrl+C)
          --url <url>          URL base da Autorizador.Api                 (padrão http://localhost:8080)
          --concorrencia <n>   máximo de requisições em andamento          (padrão 100)

        Use --terminais dentro da faixa TERMINAIS do Worker: mensagens para terminais
        sem fila são descartadas pelo RabbitMQ e a autorização fica Pendente.
        """;

    public static Opcoes Interpretar(string[] args)
    {
        var opcoes = new Opcoes(Tps: 10, Terminais: 50, Duracao: null, Url: new Uri("http://localhost:8080/"), Concorrencia: 100);

        for (var i = 0; i < args.Length; i += 2)
        {
            var nome = args[i];
            var valor = i + 1 < args.Length ? args[i + 1] : throw new ArgumentException($"Falta o valor de {nome}.");

            opcoes = nome switch
            {
                "--tps" => opcoes with { Tps = Positivo(nome, valor) },
                "--terminais" => opcoes with { Terminais = Positivo(nome, valor) },
                "--duracao" => opcoes with { Duracao = TimeSpan.FromSeconds(Positivo(nome, valor)) },
                "--url" => opcoes with { Url = UrlBase(valor) },
                "--concorrencia" => opcoes with { Concorrencia = Positivo(nome, valor) },
                _ => throw new ArgumentException($"Opção desconhecida: {nome}"),
            };
        }

        return opcoes;
    }

    private static int Positivo(string nome, string valor) =>
        int.TryParse(valor, out var n) && n > 0 ? n : throw new ArgumentException($"{nome} deve ser um inteiro maior que zero (recebido: '{valor}').");

    private static Uri UrlBase(string valor) =>
        Uri.TryCreate(valor.EndsWith('/') ? valor : valor + "/", UriKind.Absolute, out var url) && url.Scheme is "http" or "https"
            ? url
            : throw new ArgumentException($"--url inválida: '{valor}'.");
}
