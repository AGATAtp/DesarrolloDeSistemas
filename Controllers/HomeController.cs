using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using PrototipoCRUD.Data;
using PrototipoCRUD.Models;
using PrototipoCRUD.Services;

namespace PrototipoCRUD.Controllers;

public class HomeController : Controller
{
    private readonly ClienteDAO _dao;

    public HomeController(ClienteDAO dao)
    {
        _dao = dao;
    }

    // Panel principal (Dashboard)
    public IActionResult Index()
    {
        try
        {
            var (activos, nuevosMes) = _dao.Resumen();
            ViewBag.Activos = activos;
            ViewBag.NuevosMes = nuevosMes;
        }
        catch (Exception ex)
        {
            ViewBag.ErrorBD = MensajesUX.Para(ex, "HomeController.Index");
            ViewBag.Activos = 0;
            ViewBag.NuevosMes = 0;
        }
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        // Cualquier excepción no controlada llega aquí: se guarda el detalle técnico en el log
        // y el usuario solo ve una pantalla amigable (nunca el StackTrace).
        var detalle = HttpContext.Features.Get<IExceptionHandlerPathFeature>();
        if (detalle?.Error != null)
        {
            RegistroErrores.Registrar(detalle.Error, $"Excepción no controlada en {detalle.Path}");
        }
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
