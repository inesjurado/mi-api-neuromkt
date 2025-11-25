using Microsoft.EntityFrameworkCore;
using NeuromktApi.Services;
using Npgsql.EntityFrameworkCore.PostgreSQL;
using Npgsql;
using Radzen;
using NeuromktApi.Models; 

var builder = WebApplication.CreateBuilder(args);

// 1) Leer y mostrar la cadena de conexión
var cs = builder.Configuration.GetConnectionString("DefaultConnection");
Console.WriteLine(">>> DefaultConnection = " + cs);

// 2) Registrar servicios
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddHttpClient();
builder.Services.AddScoped<DialogService>();

builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseNpgsql(cs));

builder.Services.AddScoped<IEUsuario, EUsuario>();
builder.Services.AddScoped<IEProyecto, EProyecto>();
builder.Services.AddScoped<UserSession>();
builder.Services.AddScoped<IEColor, EColor>();
builder.Services.AddScoped<IEPalabra, EPalabra>();


var app = builder.Build();

// 3) Pipeline HTTP
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

// 4) Endpoint de salud de la BD
app.MapGet("/health/db", async (AppDbContext db) =>
    await db.Database.CanConnectAsync()
        ? Results.Ok("DB OK")
        : Results.Problem("DB FAIL"));

// 5) Arrancar
app.Run();




// 6) DbContext mínimo
public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> opts) : base(opts) { }
}
