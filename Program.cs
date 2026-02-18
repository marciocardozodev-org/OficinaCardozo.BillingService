using OFICINACARDOZO.BILLINGSERVICE;
using OFICINACARDOZO.BILLINGSERVICE.Messaging;
using OFICINACARDOZO.BILLINGSERVICE.Handlers;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

// Configure AppContext for Npgsql DateTime handling
AppContext.SetSwitch("Npgsql.DisableDateTimeInfinityConversions", true);
using Amazon.SQS;
using Amazon.SimpleNotificationService;
using Amazon.Runtime;

var builder = WebApplication.CreateBuilder(args);

// Configuração do JWT (chave de exemplo, troque para produção)
var jwtKey = Environment.GetEnvironmentVariable("JWT_KEY") ?? "chave-super-secreta-para-dev";
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
    };
});

// Add services to the container.
builder.Services.AddControllers(options =>
{
    options.Filters.Add<OFICINACARDOZO.BILLINGSERVICE.API.ValidationFilter>();
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "OficinaCardozo Billing Service API",
        Version = "v1",
        Description = "API para gestão de Ordens de Serviço.",
        Contact = new Microsoft.OpenApi.Models.OpenApiContact
        {
            Name = "Equipe OficinaCardozo",
            Email = "contato@oficinacardozo.com"
        }
    });
});
builder.Services.AddScoped<OFICINACARDOZO.BILLINGSERVICE.Application.PagamentoService>();
builder.Services.AddScoped<OFICINACARDOZO.BILLINGSERVICE.Application.AtualizacaoStatusOsService>();
builder.Services.AddScoped<OFICINACARDOZO.BILLINGSERVICE.Application.OrcamentoService>();
builder.Services.AddScoped<OFICINACARDOZO.BILLINGSERVICE.Application.ServiceOrchestrator>();
builder.Services.AddHealthChecks();

// AWS Messaging Configuration (mesma estratégia do OSService)
var awsAccessKeyId = Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID") ?? "";
var awsSecretAccessKey = Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY") ?? "";
var awsRegion = Environment.GetEnvironmentVariable("AWS_REGION") ?? "sa-east-1";
var sqsQueueUrl = Environment.GetEnvironmentVariable("AWS_SQS_QUEUE_BILLING") ?? "http://localhost:4566/000000000000/billing-events";

// SNS Topics Configuration (para OutboxProcessor)
var snsTopics = new SnsTopicConfiguration
{
    BudgetGeneratedTopicArn = Environment.GetEnvironmentVariable("AWS_SNS_TOPIC_BUDGETGENERATED") ?? "arn:aws:sns:sa-east-1:000000000000:budget-generated",
    BudgetApprovedTopicArn = Environment.GetEnvironmentVariable("AWS_SNS_TOPIC_BUDGETAPPROVED") ?? "arn:aws:sns:sa-east-1:000000000000:budget-approved",
    BudgetRejectedTopicArn = Environment.GetEnvironmentVariable("AWS_SNS_TOPIC_BUDGETREJECTED") ?? "arn:aws:sns:sa-east-1:000000000000:budget-rejected",
    PaymentConfirmedTopicArn = Environment.GetEnvironmentVariable("AWS_SNS_TOPIC_PAYMENTCONFIRMED") ?? "arn:aws:sns:sa-east-1:000000000000:payment-confirmed",
    PaymentFailedTopicArn = Environment.GetEnvironmentVariable("AWS_SNS_TOPIC_PAYMENTFAILED") ?? "arn:aws:sns:sa-east-1:000000000000:payment-failed",
    PaymentReversedTopicArn = Environment.GetEnvironmentVariable("AWS_SNS_TOPIC_PAYMENTREVERSED") ?? "arn:aws:sns:sa-east-1:000000000000:payment-reversed"
};
builder.Services.AddSingleton(snsTopics);

// Configurar SQS com credenciais diretas
var awsRegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(awsRegion);
var sqsConfig = new AmazonSQSConfig 
{ 
    RegionEndpoint = awsRegionEndpoint
};

var awsCredentials = new Amazon.Runtime.BasicAWSCredentials(awsAccessKeyId, awsSecretAccessKey);
builder.Services.AddSingleton<IAmazonSQS>(new AmazonSQSClient(awsCredentials, sqsConfig));

// SNS Client para OutboxProcessor
var snsConfig = new AmazonSimpleNotificationServiceConfig 
{ 
    RegionEndpoint = awsRegionEndpoint
};
builder.Services.AddSingleton<Amazon.SimpleNotificationService.IAmazonSimpleNotificationService>(
    new Amazon.SimpleNotificationService.AmazonSimpleNotificationServiceClient(awsCredentials, snsConfig));

builder.Services.AddScoped<IEventPublisher>(sp => new SqsEventPublisher(sp.GetRequiredService<IAmazonSQS>(), sqsQueueUrl));
builder.Services.AddScoped<OsCreatedHandler>();
builder.Services.AddScoped<IEventConsumer>(sp => new SqsEventConsumerImpl(
    sp.GetRequiredService<IAmazonSQS>(),
    sqsQueueUrl,
    sp.GetRequiredService<OsCreatedHandler>()
));

// ✅ Outbox Processor - Background Service que publica mensagens não entregues
builder.Services.AddHostedService<OutboxProcessor>();
builder.Services.AddHostedService<SqsEventConsumerHostedService>();

// Configuração do DbContext para PostgreSQL via variáveis de ambiente
var dbHost = Environment.GetEnvironmentVariable("DB_HOST") ?? "localhost";
var dbName = Environment.GetEnvironmentVariable("DB_NAME") ?? "billingservice";
var dbUser = Environment.GetEnvironmentVariable("DB_USER") ?? "postgres";
var dbPassword = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? "postgres";
var postgresConnectionString = $"Host={dbHost};Database={dbName};Username={dbUser};Password={dbPassword};sslmode=Require";
builder.Services.AddDbContext<BillingDbContext>(options =>
    options.UseNpgsql(postgresConnectionString, npgsqlOptions => 
    {
        npgsqlOptions.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelaySeconds: 5);
    }));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Middleware global de tratamento de exceções
app.UseMiddleware<OFICINACARDOZO.BILLINGSERVICE.API.ExceptionHandlingMiddleware>();

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");
app.Run();
