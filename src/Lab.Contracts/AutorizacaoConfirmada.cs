namespace Lab.Contracts;

/// <summary>Publicada pela API quando o terminal confirma a autorização.</summary>
public sealed record AutorizacaoConfirmada(
    Guid AutorizacaoId,
    int TerminalId,
    DateTimeOffset ConfirmadaEm);
