using Asp.Versioning;
using LampLightLabs.JobSearch.Api.Data;
using LampLightLabs.JobSearch.Api.Filters;
using LampLightLabs.JobSearch.Api.Middleware;
using LampLightLabs.JobSearch.Api.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.RateLimiting;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// API Versioning
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1);
    options.ReportApiVersions = true;
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ApiVersionReader = new UrlSegmentApiVersionReader();
})
.AddMvc()
.AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'V";
    options.SubstituteApiVersionInUrl = true;
});

// JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("JWT Key is not configured.");
var jwtIssuer = builder.Configuration["Jwt:Issuer"];
var jwtAudience = builder.Configuration["Jwt:Audience"];

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    })
    .AddScheme<AuthenticationSchemeOptions, LampLightLabs.JobSearch.Api.Authentication.BasicAuthHandler>("Basic", null);
    


// Swagger / OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "LampLightLabs.JobSearch.Api", Version = "v1" });
    options.SwaggerDoc("v2", new OpenApiInfo { Title = "LampLightLabs.JobSearch.Api", Version = "v2" });

    // JWT support in Swagger UI
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter your JWT token from POST /api/v1/auth/token"
    });
    options.AddSecurityDefinition("Basic", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "basic",
        In = ParameterLocation.Header,
        Description = "Enter username and password for Basic authentication."
    });

    options.OperationFilter<BasicAuthOperationFilter>();
    options.OperationFilter<BearerAuthOperationFilter>();

});

// Services
builder.Services.AddScoped<ICsvReaderService, CsvReaderService>();
// For testing purposes, you can swap out the real CSV reader with a JSON reader that reads from a test file.
//builder.Services.AddScoped<ICsvReaderService, JsonReaderService>();

// EF Core / Postgres — registered Scoped by AddDbContext (the default and only safe
// lifetime for a DbContext: not thread-safe, so never Singleton; wasteful to make
// Transient since one unit of work should share one instance/connection).
var pgConnectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("Postgres connection string is not configured.");
builder.Services.AddDbContext<JobSearchDbContext>(options =>
    options.UseNpgsql(pgConnectionString));

// IJobStore -> EfJobStore is the production registration (Postgres-backed). The
// original in-memory JobStore class is kept for its own unit tests and as a live
// second implementation of the same interface (Strategy Pattern, same shape as
// ICsvReaderService/JsonReaderService below) — swapping back to it is this one line.
builder.Services.AddScoped<IJobStore, EfJobStore>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IOAuthClientService, OAuthClientService>();
builder.Services.AddSingleton<IIdempotencyService, IdempotencyService>();
builder.Services.AddScoped<IStatusCategorizerService, StatusCategorizerService>();
builder.Services.AddScoped<IClaudeChatService, ClaudeChatService>();
builder.Services.AddScoped<ISemanticKernelChatService, SemanticKernelChatService>();
var openAiApiKey = builder.Configuration["OpenAI:ApiKey"]
    ?? throw new InvalidOperationException("OpenAI API key is not configured. Use user secrets or environment variables in production.");
var openAiEmbeddingModel = builder.Configuration["OpenAI:EmbeddingModel"] ?? "text-embedding-3-small";
builder.Services.AddOpenAIEmbeddingGenerator(openAiEmbeddingModel, openAiApiKey);
builder.Services.AddSingleton<ResumeVectorStoreService>();
builder.Services.AddSingleton<IResumeVectorStoreService>(sp => sp.GetRequiredService<ResumeVectorStoreService>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<ResumeVectorStoreService>());
builder.Services.AddSingleton<IPromptRepository, PromptRepository>();
builder.Services.AddScoped<IRagMatchService, RagMatchService>();

// CORS
var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? [];

builder.Services.AddCors(options =>
{
    options.AddPolicy("ViteDev", policy =>
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod());
});

// Partition by authenticated user identity, falling back to remote IP for anonymous requests.
static string GetPartitionKey(HttpContext ctx)
{
    var user = ctx.User.Identity?.Name
        ?? ctx.User.FindFirst("client_id")?.Value;
    if (!string.IsNullOrEmpty(user))
        return $"user:{user}";

    var address = ctx.Connection.RemoteIpAddress;
    if (address?.IsIPv4MappedToIPv6 == true)
        address = address.MapToIPv4();

    return $"ip:{address?.ToString() ?? "unknown"}";
}

// Rate Limiting
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.OnRejected = async (context, cancellationToken) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter =
                ((int)retryAfter.TotalSeconds).ToString(
                    System.Globalization.CultureInfo.InvariantCulture);
        }
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsync("Too Many Requests. Please retry later.", cancellationToken);
    };

    var cfg = builder.Configuration.GetSection("RateLimiting");

    // Fixed window: brute-force protection on token issuance endpoints.
    options.AddPolicy("auth-fixed", ctx =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: GetPartitionKey(ctx),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit          = cfg.GetValue("FixedWindow:PermitLimit", 10),
                Window               = TimeSpan.FromSeconds(cfg.GetValue("FixedWindow:WindowSeconds", 60)),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit           = 0,
                AutoReplenishment    = true
            }));

    // Sliding window: smooth general throttling for data endpoints.
    options.AddPolicy("api-sliding", ctx =>
        RateLimitPartition.GetSlidingWindowLimiter(
            partitionKey: GetPartitionKey(ctx),
            factory: _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit          = cfg.GetValue("SlidingWindow:PermitLimit", 100),
                Window               = TimeSpan.FromSeconds(cfg.GetValue("SlidingWindow:WindowSeconds", 60)),
                SegmentsPerWindow    = cfg.GetValue("SlidingWindow:SegmentsPerWindow", 4),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit           = 0
            }));

    // Token bucket: metered budget for expensive LLM inference endpoints.
    options.AddPolicy("ai-token-bucket", ctx =>
        RateLimitPartition.GetTokenBucketLimiter(
            partitionKey: GetPartitionKey(ctx),
            factory: _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit           = cfg.GetValue("TokenBucket:TokenLimit", 5),
                TokensPerPeriod      = cfg.GetValue("TokenBucket:TokensPerPeriod", 2),
                ReplenishmentPeriod  = TimeSpan.FromSeconds(cfg.GetValue("TokenBucket:ReplenishmentPeriodSeconds", 30)),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit           = cfg.GetValue("TokenBucket:QueueLimit", 2),
                AutoReplenishment    = true
            }));
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "v1");
    options.SwaggerEndpoint("/swagger/v2/swagger.json", "v2");
});

app.UseMiddleware<NewlineSanitizingMiddleware>();
app.UseHttpsRedirection();
app.UseRouting();           // explicit — UseRateLimiter must see endpoint metadata before MapControllers
app.UseCors("ViteDev");    // before auth so browser preflight OPTIONS is not blocked by auth
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();      // after auth so HttpContext.User is populated for partition key
app.MapControllers();
app.Run();

// Required for WebApplicationFactory in integration tests.
public partial class Program { }
