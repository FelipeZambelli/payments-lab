namespace Autorizador.Api.Autorizacoes;

/// <summary>
/// Corpo do POST /autorizacoes. Campos desconhecidos (ex.: "pan") são rejeitados com 400
/// por UnmappedMemberHandling.Disallow, configurado em Program.cs.
/// </summary>
public sealed record CriarAutorizacaoRequest(
    int TerminalId,
    decimal Valor,
    string? Bandeira,
    Guid CartaoToken,
    string? IdempotencyKey)
{
    public Dictionary<string, string[]> Validar()
    {
        var erros = new Dictionary<string, string[]>();

        if (TerminalId <= 0)
            erros["terminalId"] = ["Deve ser maior que zero."];
        if (Valor <= 0 || decimal.Round(Valor, 2) != Valor)
            erros["valor"] = ["Deve ser maior que zero e ter no máximo 2 casas decimais."];
        if (string.IsNullOrWhiteSpace(Bandeira) || Bandeira.Length > 20)
            erros["bandeira"] = ["Obrigatória, até 20 caracteres."];
        if (CartaoToken == Guid.Empty)
            erros["cartaoToken"] = ["Obrigatório (Guid não vazio)."];
        if (string.IsNullOrWhiteSpace(IdempotencyKey) || IdempotencyKey.Length > 100)
            erros["idempotencyKey"] = ["Obrigatória, até 100 caracteres."];

        return erros;
    }
}

public sealed record CriarAutorizacaoResponse(Guid AutorizacaoId);

public sealed record AutorizacaoResponse(
    Guid AutorizacaoId,
    int TerminalId,
    decimal Valor,
    string Bandeira,
    Guid CartaoToken,
    StatusAutorizacao Status,
    DateTimeOffset CriadaEm,
    DateTimeOffset? ConfirmadaEm)
{
    public static AutorizacaoResponse De(Autorizacao a) =>
        new(a.Id, a.TerminalId, a.Valor, a.Bandeira, a.CartaoToken, a.Status, a.CriadaEm, a.ConfirmadaEm);
}
