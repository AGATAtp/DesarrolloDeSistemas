using Npgsql;
using PrototipoCRUD.Models;
using PrototipoCRUD.Services;

namespace PrototipoCRUD.Data
{
    public class ReporteDAO
    {
        private readonly string _cadenaConexion;

        public ReporteDAO(IConfiguration configuracion)
        {
            _cadenaConexion = configuracion.GetConnectionString("Pepokita")
                ?? throw new InvalidOperationException("Falta la cadena de conexion 'Pepokita'.");
        }

        public List<ReporteVentaDetalle> ObtenerReporteDiario(DateTime inicio, DateTime fin)
        {
            const string sql = @"
                SELECT TO_CHAR(v.fecha, 'YYYY-MM-DD') AS periodo_grupo,
                       v.id AS nro_venta,
                       TO_CHAR(v.fecha, 'HH24:MI') AS hora,
                       v.fecha,
                       COALESCE(NULLIF(TRIM(c.nombre || ' ' || COALESCE(c.apellido, '')), ''), 'Consumidor final') AS cliente,
                       COALESCE(NULLIF(TRIM(v.vendedor), ''), 'Sin registrar') AS vendedor,
                       COALESCE(v.subtotal_bruto, v.total) AS subtotal_bruto,
                       COALESCE(v.porcentaje_descuento, 0) AS porcentaje_descuento,
                       COALESCE(v.monto_descuento, 0) AS monto_descuento,
                       COALESCE(v.total_final, v.total) AS total_final
                  FROM ventas v
                  LEFT JOIN clientes c ON c.id = v.id_cliente
                 WHERE v.fecha::date BETWEEN @inicio::date AND @fin::date
                 ORDER BY v.fecha ASC, v.id ASC";

            return EjecutarConsulta("consultar el reporte diario de ventas", conexion =>
            {
                var lista = new List<ReporteVentaDetalle>();
                using var cmd = new NpgsqlCommand(sql, conexion);
                cmd.Parameters.AddWithValue("inicio", inicio.Date);
                cmd.Parameters.AddWithValue("fin", fin.Date);
                using var lector = cmd.ExecuteReader();
                while (lector.Read())
                {
                    lista.Add(new ReporteVentaDetalle
                    {
                        PeriodoGrupo = lector.GetString(0),
                        NroVenta = lector.GetInt32(1),
                        Hora = lector.GetString(2),
                        Fecha = lector.GetDateTime(3),
                        Cliente = lector.GetString(4),
                        Vendedor = lector.GetString(5),
                        SubtotalBruto = lector.GetDecimal(6),
                        PorcentajeDescuento = lector.GetDecimal(7),
                        MontoDescuento = lector.GetDecimal(8),
                        TotalFinal = lector.GetDecimal(9)
                    });
                }
                return lista;
            });
        }

        public List<ReporteVentaMensual> ObtenerReporteMensual(DateTime inicio, DateTime fin)
        {
            const string sql = @"
                SELECT TO_CHAR(v.fecha, 'YYYY-MM') AS periodo_mes,
                       v.fecha::date AS fecha_dia,
                       COUNT(v.id) AS cantidad_ventas,
                       COALESCE(SUM(COALESCE(v.monto_descuento, 0)), 0) AS total_descuentos,
                       COALESCE(SUM(COALESCE(v.total_final, v.total)), 0) AS total_cobrado
                  FROM ventas v
                 WHERE v.fecha::date BETWEEN @inicio::date AND @fin::date
                 GROUP BY TO_CHAR(v.fecha, 'YYYY-MM'), v.fecha::date
                 ORDER BY periodo_mes ASC, fecha_dia ASC";

            return EjecutarConsulta("consultar el reporte mensual de ventas/cobranzas", conexion =>
            {
                var lista = new List<ReporteVentaMensual>();
                using var cmd = new NpgsqlCommand(sql, conexion);
                cmd.Parameters.AddWithValue("inicio", inicio.Date);
                cmd.Parameters.AddWithValue("fin", fin.Date);
                using var lector = cmd.ExecuteReader();
                while (lector.Read())
                {
                    lista.Add(new ReporteVentaMensual
                    {
                        PeriodoMes = lector.GetString(0),
                        FechaDia = lector.GetDateTime(1).Date,
                        CantidadVentas = Convert.ToInt32(lector.GetInt64(2)),
                        TotalDescuentos = lector.GetDecimal(3),
                        TotalCobrado = lector.GetDecimal(4)
                    });
                }
                return lista;
            });
        }

        private List<T> EjecutarConsulta<T>(string accion, Func<NpgsqlConnection, List<T>> consulta)
        {
            try
            {
                using var conexion = new NpgsqlConnection(_cadenaConexion);
                conexion.Open();
                return consulta(conexion);
            }
            catch (PostgresException exPg)
            {
                RegistroErrores.Registrar(exPg, $"ReporteDAO.{accion}");
                throw TraductorErroresBD.Traducir(exPg, accion);
            }
            catch (NpgsqlException exRed)
            {
                RegistroErrores.Registrar(exRed, $"ReporteDAO.{accion}");
                throw new InvalidOperationException(
                    "No pudimos consultar los datos para generar el reporte. Verifique la conexion con la base de datos.", exRed);
            }
            catch (Exception ex) when (ex is not ErrorOperacionException && ex is not InvalidOperationException)
            {
                RegistroErrores.Registrar(ex, $"ReporteDAO.{accion}");
                throw new InvalidOperationException(
                    "No pudimos generar el reporte en este momento. Intente nuevamente.", ex);
            }
        }
    }
}
