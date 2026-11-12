using System.Text.Json.Serialization;
using Autorizador.Api.Autorizacoes;
using Autorizador.Api.Banco;
using Autorizador.Api.Infra;
using Autorizador.Api.Mensageria;
using Autorizador.Api.Saude;

var builder = WebApplication.CreateBuilder(args);

var sqlConnectionString = builder.Configuration.GetConnectionString("Sql")
    ?? throw new InvalidOperationException("Configure ConnectionStrings__Sql (variável de ambiente).");
var chaos = ChaosOptions.Ler(builder.Configuration);

builder.Services.AddProblemDetails();
builder.Services.ConfigureHttpJsonOptions(o =>
{
    // Rejeita campos que não existem no contrato (ex.: alguém tentando enviar "pan").
    o.SerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(new SqlConnectionFactory(sqlConnectionString, chaos));
builder.Services.AddSingleton<AutorizacaoRepository>();
builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection("RabbitMq"));
builder.Services.AddSingleton<PublicadorRabbitMq>();
builder.Services.AddSaude();

var app = builder.Build();

if (chaos.Ativo)
    app.Logger.LogWarning("Chaos ativo: CHAOS_SQL_DELAY_MS={AtrasoMs}, CHAOS_ERROR_RATE={TaxaErro}",
        chaos.AtrasoSql.TotalMilliseconds, chaos.TaxaErro);

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseChaosDeErros(chaos);

var parando = app.Lifetime.ApplicationStopping;
await Migrador.ExecutarAsync(sqlConnectionString, app.Logger, parando);
await app.Services.GetRequiredService<PublicadorRabbitMq>().InicializarAsync(parando);

app.MapSaude();
app.MapAutorizacoes();

app.Run();
