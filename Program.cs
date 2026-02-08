using Microsoft.EntityFrameworkCore;
using NeuromktApi.Services;
using Npgsql.EntityFrameworkCore.PostgreSQL;
using Npgsql;
using Radzen;
using NeuromktApi.Models; 

var builder = WebApplication.CreateBuilder(args);

var cs = builder.Configuration.GetConnectionString("DefaultConnection");
Console.WriteLine(">>> DefaultConnection = " + cs);

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddHttpClient();
builder.Services.AddScoped<DialogService>();
builder.Services.AddScoped<TooltipService>();
builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseNpgsql(cs));

builder.Services.AddScoped<IEUsuario, EUsuario>();
builder.Services.AddScoped<IEProyecto, EProyecto>();
builder.Services.AddScoped<UserSession>();
builder.Services.AddScoped<IEColor, EColor>();
builder.Services.AddScoped<IEPalabra, EPalabra>();
builder.Services.AddScoped<IEParticipante, EParticipante>();
builder.Services.AddScoped<IEFragancia, EFragancia>();
builder.Services.AddScoped<IEProyectoFragancia, EProyectoFragancia>();
builder.Services.AddScoped<IEPrueba, EPrueba>();
builder.Services.AddScoped<IEProyectoColor, EProyectoColor>();
builder.Services.AddScoped<IEProyectoPalabra, EProyectoPalabra>();
builder.Services.AddScoped<IExportService, ExportService>();
builder.Services.AddScoped<IEResultado, EResultado>();


var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.MapRazorPages();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.MapGet("/health", () => Results.Ok("OK"));
app.MapGet("/health/db", async (AppDbContext db) =>
    await db.Database.CanConnectAsync()
        ? Results.Ok("DB OK")
        : Results.Problem("DB FAIL"));

app.Run();

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> opts) : base(opts) { }
}
