namespace Autorizador.Api.Mensageria;

/// <summary>Seção "RabbitMq" da configuração (env: RabbitMq__HostName, RabbitMq__Password, ...).</summary>
public sealed class RabbitMqOptions
{
    public string HostName { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string VirtualHost { get; set; } = "/";
    public string UserName { get; set; } = "";
    public string Password { get; set; } = "";
}
