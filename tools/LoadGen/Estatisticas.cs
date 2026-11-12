namespace LoadGen;

/// <summary>Contagem de respostas e latências: por intervalo (impresso a cada segundo) e acumulado.</summary>
public sealed class Estatisticas
{
    private readonly Lock _lock = new();
    private readonly Janela _total = new();
    private Janela _intervalo = new();

    /// <param name="status">Código HTTP, ou null se a requisição falhou (conexão recusada, timeout...).</param>
    public void Registrar(int? status, TimeSpan latencia)
    {
        lock (_lock)
        {
            _intervalo.Registrar(status, latencia);
            _total.Registrar(status, latencia);
        }
    }

    public void ImprimirIntervalo(TimeSpan decorrido)
    {
        Janela janela;
        lock (_lock)
        {
            janela = _intervalo;
            _intervalo = new Janela();
        }

        Console.WriteLine($"[{decorrido.TotalSeconds,5:0}s] {janela.Formatar()}");
    }

    public void ImprimirResumo(TimeSpan decorrido)
    {
        lock (_lock)
        {
            Console.WriteLine();
            Console.WriteLine($"Resumo ({decorrido.TotalSeconds:0.0}s, {_total.Concluidas / decorrido.TotalSeconds:0.0} req/s): {_total.Formatar()}");
        }
    }

    private sealed class Janela
    {
        private readonly List<double> _latenciasMs = [];
        private int _sucesso, _erro4xx, _erro5xx, _falhas;

        public int Concluidas => _latenciasMs.Count;

        public void Registrar(int? status, TimeSpan latencia)
        {
            _latenciasMs.Add(latencia.TotalMilliseconds);
            switch (status)
            {
                case null: _falhas++; break;
                case < 400: _sucesso++; break;
                case < 500: _erro4xx++; break;
                default: _erro5xx++; break;
            }
        }

        public string Formatar()
        {
            _latenciasMs.Sort();
            return $"concluídas {Concluidas,5} | 2xx {_sucesso,5} | 4xx {_erro4xx,4} | 5xx {_erro5xx,4} | falhas {_falhas,4}"
                 + $" | p50 {Percentil(0.50),6:0.0}ms  p95 {Percentil(0.95),6:0.0}ms  p99 {Percentil(0.99),6:0.0}ms";
        }

        /// <summary>Percentil pelo método "nearest rank" (lista já ordenada).</summary>
        private double Percentil(double p) =>
            _latenciasMs.Count == 0 ? 0 : _latenciasMs[(int)Math.Ceiling(p * _latenciasMs.Count) - 1];
    }
}
