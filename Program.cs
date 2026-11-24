using PrototipoCRUD.Data;
using PrototipoCRUD.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ClienteDAO>();
builder.Services.AddScoped<VentaDAO>();
builder.Services.AddScoped<UsuarioDAO>();
builder.Services.AddScoped<ReporteDAO>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    try
    {
        scope.ServiceProvider.GetRequiredService<ClienteDAO>().AsegurarEstructura();
        scope.ServiceProvider.GetRequiredService<VentaDAO>().AsegurarEstructura();
    }
    catch (Exception ex)
    {
        RegistroErrores.Registrar(ex, "Inicio: preparar la base de datos");
        app.Logger.LogError(ex,
            "No se pudo preparar la base de datos. Revisa que PostgreSQL esté encendido y la cadena de conexión 'Pepokita' en appsettings.json.");
    }
}

app.UseExceptionHandler("/Home/Error");
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

if (app.Urls.Any(url => url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
{
    app.UseHttpsRedirection();
}
app.UseRouting();
app.UseSession();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
