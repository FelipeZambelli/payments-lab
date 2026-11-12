namespace Lab.Contracts;

/// <summary>Nomes de exchanges, filas e routing keys compartilhados entre API e Worker.</summary>
public static class Topologia
{
    public const string Exchange = "terminais";
    public const string DeadLetterExchange = "terminais.dlx";
    public const string DeadLetterQueue = "terminais.dlq";

    public static string FilaTerminal(int terminalId) => $"terminal.{terminalId}";
    public static string RoutingKeyTerminal(int terminalId) => $"terminal.{terminalId}";
    public static string RoutingKeyConfirmacao(int terminalId) => $"confirmacao.{terminalId}";
}
