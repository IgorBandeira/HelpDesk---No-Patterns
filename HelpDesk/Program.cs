using Amazon.S3;
using HelpDesk.Data;
using HelpDesk.HostedServices;
using HelpDesk.Options;
using HelpDesk.Services;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------
// 1) Options (S3 e AWS SDK)
// ---------------------------
builder.Services.Configure<S3Options>(builder.Configuration.GetSection("S3"));
builder.Services.AddDefaultAWSOptions(builder.Configuration.GetAWSOptions());
builder.Services.AddAWSService<IAmazonS3>();

builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection("Smtp"));

// ------------------------------------
// 2) EF Core - MySQL (Pomelo provider)
// ------------------------------------
var cs = builder.Configuration.GetConnectionString("HelpDesk");
builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseMySql(cs, ServerVersion.AutoDetect(cs)));

// ------------------------------------
// 3) Services / Hosted Services (SLA)
// ------------------------------------
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<FileStorageService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddHostedService<SlaBackgroundService>();

// -----------------------
// 4) MVC / Swagger / CORS
// -----------------------
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "HelpDesk",
        Version = "v1",
        Description = "Sistema de gerenciamento completo de tickets de suporte técnico, incluindo abertura, atribuição, comentários, anexos, notificações automáticas por e-mail e monitoramento de SLA (Service Level Agreement). "
    });
    o.EnableAnnotations();

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
        o.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
});


var app = builder.Build();

// ------------------------------------
// 5) Pipeline e endpoints de diagnóstico
// ------------------------------------
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

// ------------------------------------
// 6) Health Checks
// ------------------------------------
app.MapGet("/_s3/health", async (IAmazonS3 s3) =>
{
    try
    {
        var res = await s3.ListBucketsAsync();
        return Results.Ok(new { ok = true, buckets = res.Buckets.Select(b => b.BucketName) });
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message);
    }
});

app.MapGet("/_db/health", async (AppDbContext db) =>
{
    try
    {
        var canConnect = await db.Database.CanConnectAsync();
        return canConnect ? Results.Ok(new { ok = true }) : Results.Problem("DB indisponível");
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message);
    }
});

app.MapGet("/_email/health", async (IConfiguration config) =>
{
    var host = config["Smtp:Host"];
    var port = int.TryParse(config["Smtp:Port"], out var p) ? p : 587;
    var user = config["Smtp:User"];
    var pass = config["Smtp:Password"];

    try
    {
        using var client = new MailKit.Net.Smtp.SmtpClient();
        await client.ConnectAsync(host, port, SecureSocketOptions.StartTls);
        await client.AuthenticateAsync(user, pass);
        await client.DisconnectAsync(true);

        return Results.Ok(new { ok = true, host, port, user });
    }
    catch (Exception ex)
    {
        return Results.Problem($"Erro no SMTP: {ex.Message}");
    }
});


app.Run();
