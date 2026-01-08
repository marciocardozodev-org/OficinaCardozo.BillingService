using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Microsoft.Data.SqlClient;
using Npgsql.EntityFrameworkCore.PostgreSQL;
using OficinaCardozo.Application.Interfaces;
using OficinaCardozo.Application.Mappers;
using OficinaCardozo.Application.Services;
using OficinaCardozo.Application.Settings;
using OficinaCardozo.Domain.Interfaces;
using OficinaCardozo.Infrastructure.Data;
using OficinaCardozo.Infrastructure.Repositories;
using System.Text;


Console.WriteLine("Iniciando a configuração da API Oficina Cardozo...");

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Detecta se está executando no AWS Lambda
    var isLambda = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME"));
    
    // NOTA: AddAWSLambdaHosting não é usado com APIGatewayHttpApiV2ProxyFunction
    // Quando usamos LambdaEntryPoint com APIGatewayHttpApiV2ProxyFunction,
    // o Lambda runtime gerencia automaticamente a integração
    // if (isLambda)
    // {
    //     builder.Services.AddAWSLambdaHosting(LambdaEventSource.HttpApi);
    // }

    // Configura o provedor de variáveis de ambiente para ser case-insensitive,
    // resolvendo a leitura de segredos no GitHub Codespaces.
    builder.Configuration.AddEnvironmentVariables(prefix: null);

    // Adiciona a configuração para ler segredos de arquivos (Docker Secrets),
    // útil para ambientes de produção que usam essa abordagem.
    if (builder.Environment.IsProduction())
    {
        builder.Configuration.AddKeyPerFile(directoryPath: "/run/secrets", optional: true);
    }

    var connectionStringForLog = builder.Configuration.GetConnectionString("DefaultConnection");
    var jwtKeyForLog = builder.Configuration["ConfiguracoesJwt:ChaveSecreta"];
    Console.WriteLine($"✅ ConnectionString 'DefaultConnection' carregada: {!string.IsNullOrEmpty(connectionStringForLog)}");
    if (!string.IsNullOrEmpty(connectionStringForLog))
    {
        var preview = connectionStringForLog.Length > 60 ? connectionStringForLog.Substring(0, 60) + "..." : connectionStringForLog;
        Console.WriteLine($"   Preview: {preview}");
    }
    Console.WriteLine($"✅ Chave JWT 'ConfiguracoesJwt:ChaveSecreta' carregada: {!string.IsNullOrEmpty(jwtKeyForLog)}");


    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();

    builder.Services.Configure<ConfiguracoesJwt>(builder.Configuration.GetSection("ConfiguracoesJwt"));

    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "Oficina Cardozo API",
            Version = "v1",
            Description = "API para gerenciamento da Oficina Cardozo"
        });

        c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Description = "JWT Authorization header usando o esquema Bearer. Exemplo: \"Authorization: Bearer {token}\"",
            Name = "Authorization",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.ApiKey,
            Scheme = "Bearer"
        });

        c.AddSecurityRequirement(new OpenApiSecurityRequirement()
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                },
                Scheme = "oauth2",
                Name = "Bearer",
                In = ParameterLocation.Header,
            },
            new List<string>()
        }
    });
    });

    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    Console.WriteLine($"🔍 Connection String detectada: {(string.IsNullOrEmpty(connectionString) ? "NULL/VAZIA" : connectionString.Substring(0, Math.Min(50, connectionString.Length)))}...");
    Console.WriteLine($"🌍 Ambiente: {builder.Environment.EnvironmentName}");
    Console.WriteLine($"🚀 Lambda?: {Environment.GetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME") ?? "NÃO"}");

    builder.Services.AddDbContext<OficinaDbContext>(options =>
    {
        if (connectionString != null)
        {
            // Detecta se é PostgreSQL pela connection string
            if (connectionString.Contains("Host=") || connectionString.Contains("host="))
            {
                Console.WriteLine("✅ Configurando o provedor de banco de dados para PostgreSQL.");
                Console.WriteLine($"📊 Connection String completa: {connectionString}");
                try
                {
                    options.UseNpgsql(connectionString,
                        npgsqlOptions => npgsqlOptions.MigrationsAssembly(typeof(OficinaDbContext).Assembly.FullName));
                    Console.WriteLine("✅ PostgreSQL configurado com sucesso!");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ ERRO ao configurar PostgreSQL: {ex.Message}");
                    Console.WriteLine($"❌ StackTrace: {ex.StackTrace}");
                    throw;
                }
            }
            else
            {
                // Usa SQLite para ambientes locais
                Console.WriteLine("✅ Configurando o provedor de banco de dados para SQLite.");
                var dbPath = connectionString.Contains("Data Source=") ? connectionString.Split('=')[1] : connectionString;
                var dbFolder = Path.GetDirectoryName(dbPath);
                if (!string.IsNullOrEmpty(dbFolder) && !Directory.Exists(dbFolder))
                {
                    Console.WriteLine($"📁 Criando diretório para o banco de dados SQLite em: {dbFolder}");
                    Directory.CreateDirectory(dbFolder);
                }
                var sqliteConnectionString = connectionString.Contains("Data Source=") ? connectionString : $"Data Source={connectionString}";
                options.UseSqlite(sqliteConnectionString,
                    sqliteOptions => sqliteOptions.MigrationsAssembly(typeof(OficinaDbContext).Assembly.FullName));
            }
        }
        else
        {
            Console.WriteLine("❌ ERRO: Connection string não encontrada!");
            throw new InvalidOperationException("A string de conexão 'DefaultConnection' não foi encontrada.");
        }

        if (builder.Environment.IsDevelopment())
        {
            options.LogTo(Console.WriteLine, Microsoft.Extensions.Logging.LogLevel.Information);
            options.EnableDetailedErrors();
            options.EnableSensitiveDataLogging();
        }
    });


    var jwtKey = builder.Configuration["ConfiguracoesJwt:ChaveSecreta"];
    if (string.IsNullOrEmpty(jwtKey))
    {
        throw new InvalidOperationException("JWT Key não foi configurada. Verifique os segredos do Codespaces (ConfiguracoesJwt__ChaveSecreta) ou os segredos do Docker.");
    }

    var key = Encoding.ASCII.GetBytes(jwtKey);

    builder.Services.AddAuthentication(x =>
    {
        x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(x =>
    {
        x.RequireHttpsMetadata = false;
        x.SaveToken = true;
        x.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });

    builder.Services.AddScoped<IUsuarioRepository, UsuarioRepository>();
    builder.Services.AddScoped<IClienteRepository, ClienteRepository>();
    builder.Services.AddScoped<IVeiculoRepository, VeiculoRepository>();
    builder.Services.AddScoped<IServicoRepository, ServicoRepository>();
    builder.Services.AddScoped<IPecaRepository, PecaRepository>();
    builder.Services.AddScoped<IOrdemServicoRepository, OrdemServicoRepository>();
    builder.Services.AddScoped<IOrcamentoRepository, OrcamentoRepository>();
    builder.Services.AddScoped<IOrdemServicoStatusRepository, OrdemServicoStatusRepository>();
    builder.Services.AddScoped<IOrcamentoStatusRepository, OrcamentoStatusRepository>();

    builder.Services.AddScoped<IClienteMapper, ClienteMapper>();
    builder.Services.AddScoped<IVeiculoMapper, VeiculoMapper>();
    builder.Services.AddScoped<IServicoMapper, ServicoMapper>();


    builder.Services.AddScoped<IAutenticacaoService, AutenticacaoService>();
    builder.Services.AddScoped<IClienteService, ClienteService>();
    builder.Services.AddScoped<IVeiculoService, VeiculoService>();
    builder.Services.AddScoped<IServicoService, ServicoService>();
    builder.Services.AddScoped<IPecaService, PecaService>();
    builder.Services.AddScoped<IOrdemServicoService, OrdemServicoService>();
    builder.Services.AddScoped<ICpfCnpjValidationService, CpfCnpjValidationService>();

    builder.Services.Configure<ConfiguracoesEmail>(
        builder.Configuration.GetSection("ConfiguracoesEmail"));

    builder.Services.AddScoped<IOrdemServicoStatusService, OrdemServicoStatusService>();
    builder.Services.AddScoped<IEmailMonitorService, EmailMonitorService>();

    builder.Services.AddHostedService<EmailMonitorService>();

    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowAll",
            builder =>
            {
                builder
                    .AllowAnyOrigin()
                    .AllowAnyMethod()
                    .AllowAnyHeader();
            });
    });

    var app = builder.Build();

    Console.WriteLine("📋 Configurando Swagger...");
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Oficina Cardozo API v1");
        c.RoutePrefix = "swagger";
        c.DocumentTitle = "Oficina Cardozo API - Swagger UI";
    });

    // Logging de requisições para diagnóstico (Lambda CloudWatch)
    if (isLambda)
    {
        app.Use(async (context, next) =>
        {
            var path = context.Request.Path.Value ?? "/";
            var method = context.Request.Method;
            Console.WriteLine($"🔍 [{method}] {path}");
            
            await next();
            
            var statusCode = context.Response.StatusCode;
            var statusEmoji = statusCode >= 200 && statusCode < 300 ? "✅" : 
                             statusCode >= 400 && statusCode < 500 ? "⚠️" : "❌";
            Console.WriteLine($"{statusEmoji} [{method}] {path} → {statusCode}");
        });
    }

    Console.WriteLine("🔐 Configurando CORS, Authentication e Authorization...");
    app.UseCors("AllowAll");
    
    // CRÍTICO: UseRouting deve vir antes de UseAuthentication/UseAuthorization
    app.UseRouting();
    
    app.UseAuthentication();
    app.UseAuthorization();
    
    // MapControllers deve vir DEPOIS de UseRouting
    app.MapControllers();

    Console.WriteLine("✅ Aplicação configurada e pronta para iniciar.");

    app.Run();

}
catch (Exception ex)
{
    Console.WriteLine($"💥 ERRO FATAL: A aplicação falhou ao iniciar. {ex.Message}");
    Console.WriteLine($"💥 Stack Trace completo:");
    Console.WriteLine(ex.ToString());
    Environment.Exit(1);
}

// Torna a classe Program acessível para AWS Lambda
// Necessário para ASP.NET Core 6+ com minimal APIs
public partial class Program { }
