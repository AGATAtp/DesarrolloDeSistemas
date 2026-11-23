using System.Data;

namespace PrototipoCRUD.Models
{
    public class ReporteVentaDetalle
    {
        public string PeriodoGrupo { get; set; } = string.Empty;
        public int NroVenta { get; set; }
        public string Hora { get; set; } = string.Empty;
        public DateTime Fecha { get; set; }
        public string Cliente { get; set; } = string.Empty;
        public string Vendedor { get; set; } = string.Empty;
        public decimal SubtotalBruto { get; set; }
        public decimal PorcentajeDescuento { get; set; }
        public decimal MontoDescuento { get; set; }
        public decimal TotalFinal { get; set; }
    }

    public class ReporteVentaMensual
    {
        public string PeriodoMes { get; set; } = string.Empty;
        public DateTime FechaDia { get; set; }
        public int CantidadVentas { get; set; }
        public decimal TotalDescuentos { get; set; }
        public decimal TotalCobrado { get; set; }
    }

    public class ReporteVentasGrupo<T>
    {
        public string Clave { get; set; } = string.Empty;
        public string Titulo { get; set; } = string.Empty;
        public List<T> Items { get; set; } = new();
        public int CantidadOperaciones { get; set; }
        public decimal TotalDescuentos { get; set; }
        public decimal TotalCobrado { get; set; }
    }

    public class ReporteVentasViewModel
    {
        public DateTime FechaInicio { get; set; } = DateTime.Today;
        public DateTime FechaFin { get; set; } = DateTime.Today;
        public string TipoGranularidad { get; set; } = "Diario";
        public bool Consultado { get; set; }
        public string? Error { get; set; }
        public string? MensajeSinDatos { get; set; }

        public List<ReporteVentasGrupo<ReporteVentaDetalle>> GruposDiarios { get; set; } = new();
        public List<ReporteVentasGrupo<ReporteVentaMensual>> GruposMensuales { get; set; } = new();

        public int GranTotalOperaciones { get; set; }
        public decimal GranTotalDescuentos { get; set; }
        public decimal GranTotalCobrado { get; set; }

        public bool TieneDatos => TipoGranularidad == "Mensual"
            ? GruposMensuales.Count > 0
            : GruposDiarios.Count > 0;
    }
}
