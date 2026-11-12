namespace Autorizador.Api.Autorizacoes;

public enum StatusAutorizacao
{
    Pendente,
    Confirmada,
}

/// <summary>Linha da tabela dbo.Autorizacoes. Não existe PAN aqui: o cartão é só um token (Guid).</summary>
public sealed class Autorizacao
{
    public Guid Id { get; init; }
    public string IdempotencyKey { get; init; } = "";
    public int TerminalId { get; init; }
    public decimal Valor { get; init; }
    public string Bandeira { get; init; } = "";
    public Guid CartaoToken { get; init; }
    public StatusAutorizacao Status { get; init; }
    public DateTimeOffset CriadaEm { get; init; }
    public DateTimeOffset? PublicadaEm { get; init; }
    public DateTimeOffset? ConfirmadaEm { get; init; }
}
