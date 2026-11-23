using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using PrototipoCRUD.Data;
using PrototipoCRUD.Models;
using PrototipoCRUD.Services;

namespace PrototipoCRUD.Controllers
{
    public class ReporteController : Controller
    {
        private readonly ReporteDAO _reporteDao;

        public ReporteController(ReporteDAO reporteDao)
        {
            _reporteDao = reporteDao;
        }

        [HttpGet]
        public IActionResult VentasCobranzas(DateTime? fechaInicio, DateTime? fechaFin, string? tipoGranularidad)
        {
            var sesion = ObtenerSesion();
            if (sesion == null)
                return RedirectToAction("Login", "Account", new { returnUrl = "/Reporte/VentasCobranzas" });

            var modelo = new ReporteVentasViewModel
            {
                FechaInicio = (fechaInicio ?? DateTime.Today).Date,
                FechaFin = (fechaFin ?? DateTime.Today).Date,
                TipoGranularidad = NormalizarGranularidad(tipoGranularidad)
            };

            if (fechaInicio == null && fechaFin == null && string.IsNullOrWhiteSpace(tipoGranularidad))
                return View(modelo);

            if (modelo.FechaInicio > modelo.FechaFin)
            {
                modelo.Error = "La fecha de inicio no puede ser mayor que la fecha fin. Ajuste el rango e intente nuevamente.";
                return View(modelo);
            }

            modelo.Consultado = true;
            ViewBag.UsuarioGenera = sesion.NombreCompleto;
            ViewBag.FechaEmision = DateTime.Now;

            try
            {
                if (modelo.TipoGranularidad == "Mensual")
                    CargarMensual(modelo);
                else
                    CargarDiario(modelo);

                if (!modelo.TieneDatos)
                {
                    modelo.MensajeSinDatos =
                        "No existen ventas registradas en el rango de fechas seleccionado. Intente ampliar el periodo de busqueda.";
                }
            }
            catch (Exception ex)
            {
                RegistroErrores.Registrar(ex, "ReporteController.VentasCobranzas");
                modelo.Error = ex is ErrorOperacionException || ex is InvalidOperationException
                    ? ex.Message
                    : "No pudimos generar el reporte en este momento. Intente nuevamente.";
            }

            return View(modelo);
        }

        private void CargarDiario(ReporteVentasViewModel modelo)
        {
            var datos = _reporteDao.ObtenerReporteDiario(modelo.FechaInicio, modelo.FechaFin);
            var cultura = CultureInfo.GetCultureInfo("es-ES");
            modelo.GruposDiarios = datos
                .GroupBy(d => d.PeriodoGrupo)
                .OrderBy(g => g.Key)
                .Select(g =>
                {
                    var fecha = g.First().Fecha.Date;
                    return new ReporteVentasGrupo<ReporteVentaDetalle>
                    {
                        Clave = g.Key,
                        Titulo = fecha.ToString("dddd dd/MM/yyyy", cultura),
                        Items = g.OrderBy(x => x.Fecha).ThenBy(x => x.NroVenta).ToList(),
                        CantidadOperaciones = g.Count(),
                        TotalDescuentos = g.Sum(x => x.MontoDescuento),
                        TotalCobrado = g.Sum(x => x.TotalFinal)
                    };
                })
                .ToList();

            modelo.GranTotalOperaciones = modelo.GruposDiarios.Sum(g => g.CantidadOperaciones);
            modelo.GranTotalDescuentos = modelo.GruposDiarios.Sum(g => g.TotalDescuentos);
            modelo.GranTotalCobrado = modelo.GruposDiarios.Sum(g => g.TotalCobrado);
        }

        private void CargarMensual(ReporteVentasViewModel modelo)
        {
            var datos = _reporteDao.ObtenerReporteMensual(modelo.FechaInicio, modelo.FechaFin);
            var cultura = CultureInfo.GetCultureInfo("es-ES");
            modelo.GruposMensuales = datos
                .GroupBy(d => d.PeriodoMes)
                .OrderBy(g => g.Key)
                .Select(g =>
                {
                    var primero = g.First().FechaDia;
                    var tituloMes = primero.ToString("MMMM yyyy", cultura);
                    return new ReporteVentasGrupo<ReporteVentaMensual>
                    {
                        Clave = g.Key,
                        Titulo = char.ToUpper(tituloMes[0], cultura) + tituloMes[1..],
                        Items = g.OrderBy(x => x.FechaDia).ToList(),
                        CantidadOperaciones = g.Sum(x => x.CantidadVentas),
                        TotalDescuentos = g.Sum(x => x.TotalDescuentos),
                        TotalCobrado = g.Sum(x => x.TotalCobrado)
                    };
                })
                .ToList();

            modelo.GranTotalOperaciones = modelo.GruposMensuales.Sum(g => g.CantidadOperaciones);
            modelo.GranTotalDescuentos = modelo.GruposMensuales.Sum(g => g.TotalDescuentos);
            modelo.GranTotalCobrado = modelo.GruposMensuales.Sum(g => g.TotalCobrado);
        }

        private static string NormalizarGranularidad(string? tipo)
        {
            return string.Equals(tipo, "Mensual", StringComparison.OrdinalIgnoreCase) ? "Mensual" : "Diario";
        }

        private UsuarioSesion? ObtenerSesion()
        {
            var nombre = HttpContext.Session.GetString("UsuarioNombre");
            if (string.IsNullOrWhiteSpace(nombre)) return null;
            return new UsuarioSesion
            {
                NombreUsuario = nombre,
                NombreCompleto = HttpContext.Session.GetString("NombreCompleto") ?? nombre
            };
        }

        private class UsuarioSesion
        {
            public string NombreUsuario { get; set; } = string.Empty;
            public string NombreCompleto { get; set; } = string.Empty;
        }
    }
}
