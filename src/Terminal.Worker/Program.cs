using Lab.Telemetria;
using Terminal.Worker.Configuracao;
using Terminal.Worker.Mensageria;
using Terminal.Worker.Processamento;

var builder = Host.CreateApplicationBuilder(args);
builder.AddTelemetria();

var opcoes = WorkerOptions.Ler(builder.Configuration);
builder.Services.AddSingleton(opcoes);
builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection("RabbitMq"));

builder.Services.AddHttpClient(ProcessadorAutorizacao.ClienteApi, http =>
{
    http.BaseAddress = opcoes.ApiUrl;
    http.Timeout = TimeSpan.FromSeconds(10);
});
builder.Services.AddSingleton<ProcessadorAutorizacao>();
builder.Services.AddHostedService<ConsumidorTerminais>();

// Prazo para terminar as mensagens em voo após SIGTERM. O Compose espera 30s
// (stop_grace_period) antes de mandar SIGKILL, então este valor precisa ser menor.
builder.Services.Configure<HostOptions>(o => o.ShutdownTimeout = TimeSpan.FromSeconds(25));

builder.Build().Run();
