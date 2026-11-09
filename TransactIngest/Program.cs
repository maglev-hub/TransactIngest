using Microsoft.EntityFrameworkCore;
using TransactIngest.Application.Interfaces;
using TransactIngest.Application.Services;
using TransactIngest.Infrastructure.Data;
using TransactIngest.Infrastructure.Repositories;
using TransactIngest.Infrastructure.Sources;
using static TransactIngest.Infrastructure.Sources.InMemoryTransactionSource;
using TransactIngest.ExceptionHandling;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var connectionString =
    builder.Configuration.GetConnectionString(
        "DefaultConnection")
    ?? throw new InvalidOperationException(
        "DefaultConnection is not configured.");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(connectionString));

builder.Services.AddScoped<
    ITransactionRepository,
    TransactionRepository>();

builder.Services.AddScoped<
    IImportService,
    ImportService>();

builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);

builder.Services.AddOptions<MockSourceOptions>(SourceSystems.Downtown)
    .Bind(builder.Configuration.GetSection("MockSources:Downtown"));

builder.Services.AddOptions<MockSourceOptions>(SourceSystems.Online)
    .Bind(builder.Configuration.GetSection("MockSources:Online"));

builder.Services.AddOptions<MockSourceOptions>(SourceSystems.Uptown)
    .Bind(builder.Configuration.GetSection("MockSources:Uptown"));

builder.Services.AddSingleton<ITransactionSource, DowntownTransactionSource>();
builder.Services.AddSingleton<ITransactionSource, OnlineTransactionSource>();
builder.Services.AddSingleton<ITransactionSource, UptownTransactionSource>();

builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AngularPolicy", policy =>
    {
        policy.WithOrigins("http://localhost:4200",
             "http://localhost:60291")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddProblemDetails();

builder.Services.AddExceptionHandler<
    GlobalExceptionHandler>();

var app = builder.Build();

//_ = app.Services.GetServices<ITransactionSource>().ToArray();

app.UseExceptionHandler();


app.UseCors("AngularPolicy");

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
