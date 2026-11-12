namespace Lab.Contracts;

/// <summary>Publicada pela API quando uma autorização é registrada; entregue ao terminal.</summary>
public sealed record AutorizacaoCriada(
    Guid AutorizacaoId,
    int TerminalId,
    decimal Valor,
    string Bandeira,
    Guid CartaoToken,
    DateTimeOffset CriadaEm);
