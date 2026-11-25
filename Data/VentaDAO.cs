using Npgsql;
using NpgsqlTypes;
using PrototipoCRUD.Models;
using PrototipoCRUD.Services;

namespace PrototipoCRUD.Data
{
    public class VentaDAO
    {
        private readonly string _cadenaConexion;

        public VentaDAO(IConfiguration configuracion)
        {
            _cadenaConexion = configuracion.GetConnectionString("Pepokita")
                ?? throw new InvalidOperationException("Falta la cadena de conexión 'Pepokita'.");
        }

        public void AsegurarEstructura()
        {
            const string sql = @"
                CREATE TABLE IF NOT EXISTS productos (
                    id     SERIAL PRIMARY KEY,
                    nombre VARCHAR(100)  NOT NULL UNIQUE,
                    precio NUMERIC(10,2) NOT NULL CHECK (precio >= 0),
                    activo BOOLEAN       NOT NULL DEFAULT TRUE
                );
                CREATE TABLE IF NOT EXISTS ventas (
                    id         SERIAL PRIMARY KEY,
                    fecha      TIMESTAMP     NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    id_cliente INT           NOT NULL REFERENCES clientes(id) ON DELETE RESTRICT,
                    total      NUMERIC(10,2) NOT NULL CHECK (total >= 0)
                );
                CREATE TABLE IF NOT EXISTS detalle_ventas (
                    id              SERIAL PRIMARY KEY,
                    id_venta        INT           NOT NULL REFERENCES ventas(id) ON DELETE CASCADE,
                    id_producto     INT           NOT NULL REFERENCES productos(id) ON DELETE RESTRICT,
                    cantidad        INT           NOT NULL CHECK (cantidad > 0),
                    precio_unitario NUMERIC(10,2) NOT NULL CHECK (precio_unitario >= 0),
                    subtotal        NUMERIC(10,2) NOT NULL CHECK (subtotal = ROUND(cantidad * precio_unitario, 2))
                );
                INSERT INTO productos (nombre, precio)
                SELECT v.nombre, v.precio
                FROM (VALUES
                    ('Lavado Exterior', 30.00),
                    ('Lavado Completo', 60.00),
                    ('Encerado y Pulido', 90.00),
                    ('Limpieza de Tapiz', 75.00),
                    ('Shampoo de Motor', 50.00),
                    ('Aromatizacion', 10.00),
                    ('Aspirado Interior', 20.00),
                    ('Sanitizado Interior', 35.00)
                ) AS v(nombre, precio)
                WHERE NOT EXISTS (SELECT 1 FROM productos);

                -- CR-01: usuarios / rol / descuentos y trazabilidad
                CREATE TABLE IF NOT EXISTS usuarios (
                    id                SERIAL PRIMARY KEY,
                    empleado_id       INT          NOT NULL UNIQUE REFERENCES empleados(id) ON DELETE RESTRICT,
                    nombre_usuario    VARCHAR(50)  NOT NULL UNIQUE,
                    contrasena_hash   VARCHAR(255) NOT NULL,
                    activo            BOOLEAN      NOT NULL DEFAULT TRUE,
                    intentos_fallidos INT          NOT NULL DEFAULT 0 CHECK (intentos_fallidos >= 0),
                    bloqueado         BOOLEAN      NOT NULL DEFAULT FALSE,
                    ultimo_acceso     TIMESTAMP
                );
                ALTER TABLE usuarios ADD COLUMN IF NOT EXISTS intentos_fallidos INT     NOT NULL DEFAULT 0;
                ALTER TABLE usuarios ADD COLUMN IF NOT EXISTS bloqueado         BOOLEAN NOT NULL DEFAULT FALSE;
                ALTER TABLE usuarios ADD COLUMN IF NOT EXISTS ultimo_acceso     TIMESTAMP;
                ALTER TABLE usuarios DROP CONSTRAINT IF EXISTS usuarios_intentos_fallidos_check;
                ALTER TABLE usuarios ADD CONSTRAINT usuarios_intentos_fallidos_check CHECK (intentos_fallidos >= 0);

                INSERT INTO roles (nombre) VALUES ('Vendedor') ON CONFLICT (nombre) DO NOTHING;

                ALTER TABLE ventas ADD COLUMN IF NOT EXISTS vendedor              VARCHAR(100)  NOT NULL DEFAULT 'Sin registrar';
                ALTER TABLE ventas ADD COLUMN IF NOT EXISTS id_vendedor           INT           REFERENCES empleados(id) ON DELETE RESTRICT;
                ALTER TABLE ventas ADD COLUMN IF NOT EXISTS subtotal_bruto        NUMERIC(10,2);
                ALTER TABLE ventas ADD COLUMN IF NOT EXISTS porcentaje_descuento  NUMERIC(5,2)  NOT NULL DEFAULT 0;
                ALTER TABLE ventas ADD COLUMN IF NOT EXISTS monto_descuento       NUMERIC(10,2) NOT NULL DEFAULT 0;
                ALTER TABLE ventas ADD COLUMN IF NOT EXISTS total_final           NUMERIC(10,2);

                UPDATE ventas
                   SET subtotal_bruto = COALESCE(subtotal_bruto, total),
                       total_final    = COALESCE(total_final, total),
                       vendedor       = COALESCE(NULLIF(BTRIM(vendedor), ''), 'Sin registrar'),
                       porcentaje_descuento = COALESCE(porcentaje_descuento, 0),
                       monto_descuento      = COALESCE(monto_descuento, 0)
                 WHERE subtotal_bruto IS NULL OR total_final IS NULL OR vendedor IS NULL OR BTRIM(vendedor) = '';

                ALTER TABLE ventas ALTER COLUMN subtotal_bruto SET NOT NULL;
                ALTER TABLE ventas ALTER COLUMN total_final   SET NOT NULL;

                ALTER TABLE ventas DROP CONSTRAINT IF EXISTS ck_ventas_descuento_rango;
                ALTER TABLE ventas ADD CONSTRAINT ck_ventas_descuento_rango
                    CHECK (porcentaje_descuento >= 0 AND porcentaje_descuento <= 20);
                ALTER TABLE ventas DROP CONSTRAINT IF EXISTS ck_ventas_montos_coherentes;
                ALTER TABLE ventas ADD CONSTRAINT ck_ventas_montos_coherentes CHECK (
                    subtotal_bruto >= 0
                    AND monto_descuento = ROUND(subtotal_bruto * porcentaje_descuento / 100, 2)
                    AND total_final     = subtotal_bruto - monto_descuento
                    AND total           = total_final
                );
                ALTER TABLE ventas DROP CONSTRAINT IF EXISTS ck_ventas_vendedor;
                ALTER TABLE ventas ADD CONSTRAINT ck_ventas_vendedor CHECK (
                    LENGTH(BTRIM(vendedor)) > 0
                    AND (porcentaje_descuento = 0 OR vendedor <> 'Sin registrar')
                );

                CREATE INDEX IF NOT EXISTS idx_ventas_vendedor    ON ventas (vendedor);
                CREATE INDEX IF NOT EXISTS idx_ventas_id_vendedor ON ventas (id_vendedor);

                INSERT INTO empleados (rol_id, documento, nombre, apellido, telefono, activo)
                SELECT r.id, 'VEN-0001', 'Carlos', 'Perez', '70000001', TRUE
                  FROM roles r WHERE r.nombre = 'Vendedor'
                ON CONFLICT (documento) DO NOTHING;

                INSERT INTO usuarios (empleado_id, nombre_usuario, contrasena_hash, activo, intentos_fallidos, bloqueado)
                SELECT e.id, 'cperez', 'v1$JU6Ff5stfcumJuRafzaB2Q==$pOnR6xCWUYsaLgtoDEONPxrl1xxRZVcTKB6Aa6V87Lg=', TRUE, 0, FALSE
                  FROM empleados e WHERE e.documento = 'VEN-0001'
                ON CONFLICT (nombre_usuario) DO NOTHING;
            ";
            using var conexion = new NpgsqlConnection(_cadenaConexion);
            conexion.Open();
            using var comando = new NpgsqlCommand(sql, conexion);
            comando.ExecuteNonQuery();
        }

        public int ObtenerIdConsumidorFinal()
        {
            return ProteccionBD.Ejecutar<int>("Venta.ObtenerIdConsumidorFinal", "preparar la venta", () =>
            {
                const string documento = "00000000-CF";
                using var conexion = new NpgsqlConnection(_cadenaConexion);
                conexion.Open();

                using (var insercion = new NpgsqlCommand(
                    @"INSERT INTO clientes (documento, nombre, apellido, telefono, tipo_cliente)
                      VALUES (@documento, 'Consumidor', 'Final', '00000000', 'Particular')
                      ON CONFLICT (documento) DO NOTHING", conexion))
                {
                    insercion.Parameters.AddWithValue("documento", documento);
                    insercion.ExecuteNonQuery();
                }

                using var consulta = new NpgsqlCommand(
                    "SELECT id FROM clientes WHERE documento = @documento", conexion);
                consulta.Parameters.AddWithValue("documento", documento);
                return Convert.ToInt32(consulta.ExecuteScalar());
            });
        }

        public List<Producto> ObtenerProductos()
        {
            return ProteccionBD.Ejecutar<List<Producto>>("Venta.ObtenerProductos", "mostrar el catálogo de servicios", () =>
            {
                var lista = new List<Producto>();
                const string sql = "SELECT id, nombre, precio, activo FROM productos WHERE activo = TRUE ORDER BY nombre";
                using var conexion = new NpgsqlConnection(_cadenaConexion);
                conexion.Open();
                using var comando = new NpgsqlCommand(sql, conexion);
                using var lector = comando.ExecuteReader();
                while (lector.Read())
                {
                    lista.Add(new Producto
                    {
                        Id = lector.GetInt32(0),
                        Nombre = lector.GetString(1),
                        Precio = lector.GetDecimal(2),
                        Activo = lector.GetBoolean(3)
                    });
                }
                return lista;
            });
        }

        public int RegistrarVenta(Venta venta)
        {
            // ---------- Cláusulas de guarda ----------
            if (venta == null)
                throw new ValidacionNegocioException("No recibimos los datos de la venta. Vuelve a armar la venta e inténtalo de nuevo.");
            if (venta.IdCliente < 0)
                throw new ValidacionNegocioException("El cliente seleccionado no es válido. Elige un cliente de la lista o deja 'Consumidor final'.");
            if (venta.Detalles == null || venta.Detalles.Count == 0)
                throw new ValidacionNegocioException("Agrega al menos un servicio antes de confirmar la venta.");
            if (venta.Detalles.Any(d => d.Cantidad < 1 || d.PrecioUnitario < 0))
                throw new ValidacionNegocioException("Hay un servicio con cantidad o precio no válido. Revisa el detalle de la venta.");

            int idCliente = venta.IdCliente != 0 ? venta.IdCliente : ObtenerIdConsumidorFinal();

            NpgsqlConnection? conexion = null;
            NpgsqlTransaction? transaccion = null;
            bool confirmada = false;

            try
            {
                conexion = new NpgsqlConnection(_cadenaConexion);
                conexion.Open();
                transaccion = conexion.BeginTransaction();

                const string sqlCabecera = @"
                    INSERT INTO ventas (fecha, id_cliente, total, vendedor, id_vendedor,
                                        subtotal_bruto, porcentaje_descuento, monto_descuento, total_final)
                    VALUES (@fecha, @idCliente, @total, @vendedor, @idVendedor,
                            @subtotalBruto, @porcentajeDescuento, @montoDescuento, @totalFinal)
                    RETURNING id";
                int idVentaGenerado;

                using (var cmdCab = new NpgsqlCommand(sqlCabecera, conexion, transaccion))
                {
                    cmdCab.Parameters.Add(new NpgsqlParameter("fecha", NpgsqlDbType.Timestamp)
                    {
                        Value = DateTime.SpecifyKind(venta.Fecha, DateTimeKind.Unspecified)
                    });
                    cmdCab.Parameters.AddWithValue("idCliente", idCliente);
                    cmdCab.Parameters.AddWithValue("total", venta.TotalFinal);
                    cmdCab.Parameters.AddWithValue("vendedor", venta.Vendedor ?? string.Empty);
                    cmdCab.Parameters.AddWithValue("idVendedor", (object?)venta.IdVendedor ?? DBNull.Value);
                    cmdCab.Parameters.AddWithValue("subtotalBruto", venta.SubtotalBruto);
                    cmdCab.Parameters.AddWithValue("porcentajeDescuento", venta.PorcentajeDescuento);
                    cmdCab.Parameters.AddWithValue("montoDescuento", venta.MontoDescuento);
                    cmdCab.Parameters.AddWithValue("totalFinal", venta.TotalFinal);
                    idVentaGenerado = Convert.ToInt32(cmdCab.ExecuteScalar());
                }

                const string sqlDetalle = @"
                    INSERT INTO detalle_ventas (id_venta, id_producto, cantidad, precio_unitario, subtotal)
                    VALUES (@idVenta, @idProd, @cant, @precio, @sub)";

                foreach (var item in venta.Detalles)
                {
                    using var cmdDet = new NpgsqlCommand(sqlDetalle, conexion, transaccion);
                    cmdDet.Parameters.AddWithValue("idVenta", idVentaGenerado);
                    cmdDet.Parameters.AddWithValue("idProd", item.IdProducto);
                    cmdDet.Parameters.AddWithValue("cant", item.Cantidad);
                    cmdDet.Parameters.AddWithValue("precio", item.PrecioUnitario);
                    cmdDet.Parameters.AddWithValue("sub", item.Subtotal);
                    cmdDet.ExecuteNonQuery();
                }

                transaccion.Commit();
                confirmada = true;
                return idVentaGenerado;
            }
            catch (PostgresException exPg)
            {
                RegistroErrores.Registrar(exPg, "Venta.RegistrarVenta");
                if (exPg.SqlState == "23503")   // FOREIGN KEY: cliente o servicio ya no existe
                    throw new ErrorOperacionException(
                        "No se registró la venta porque el cliente o alguno de los servicios ya no está disponible. Actualiza la página, revisa la selección e inténtalo de nuevo; no se guardó nada.", exPg);
                throw TraductorErroresBD.Traducir(exPg, "registrar la venta");
            }
            catch (NpgsqlException exRed)
            {
                RegistroErrores.Registrar(exRed, "Venta.RegistrarVenta");
                throw TraductorErroresBD.ErrorConexion(exRed);
            }
            catch (Exception ex)
            {
                RegistroErrores.Registrar(ex, "Venta.RegistrarVenta");
                throw;
            }
            finally
            {
                // Limpieza garantizada: si no se confirmó, se deshace todo (maestro y detalle)
                if (transaccion != null && !confirmada)
                    RevertirSinFallar(transaccion);
                transaccion?.Dispose();
                conexion?.Dispose();
            }
        }

        private static void RevertirSinFallar(NpgsqlTransaction transaccion)
        {
            try
            {
                transaccion.Rollback();
            }
            catch (Exception exRollback)
            {
                // La conexión pudo haberse caído: el servidor descarta la transacción por su cuenta.
                RegistroErrores.Registrar(exRollback, "Venta.RegistrarVenta (rollback)");
            }
        }

        public List<Cliente> ObtenerClientesConCompras()
        {
            return ProteccionBD.Ejecutar<List<Cliente>>("Venta.ObtenerClientesConCompras", "mostrar los clientes con compras", () =>
            {
                var lista = new List<Cliente>();
                const string sql = @"
                    SELECT DISTINCT c.id, c.documento, c.nombre, c.apellido
                      FROM clientes c
                      JOIN ventas v ON v.id_cliente = c.id
                     ORDER BY c.apellido, c.nombre";
                using var conexion = new NpgsqlConnection(_cadenaConexion);
                conexion.Open();
                using var comando = new NpgsqlCommand(sql, conexion);
                using var lector = comando.ExecuteReader();
                while (lector.Read())
                {
                    lista.Add(new Cliente
                    {
                        Id = lector.GetInt32(0),
                        Documento = lector.IsDBNull(1) ? string.Empty : lector.GetString(1),
                        Nombre = lector.IsDBNull(2) ? string.Empty : lector.GetString(2),
                        Apellido = lector.IsDBNull(3) ? string.Empty : lector.GetString(3)
                    });
                }
                return lista;
            });
        }

        public List<VentaHistorial> ObtenerHistorial() => ObtenerHistorial(null, null, 0);

        public List<VentaHistorial> ObtenerHistorial(DateOnly? desde, DateOnly? hasta, int idCliente)
        {
            return ProteccionBD.Ejecutar<List<VentaHistorial>>("Venta.ObtenerHistorial", "mostrar el historial de ventas", () =>
            {
                var sql = @"
                    SELECT v.id, v.fecha, v.id_cliente,
                           COALESCE(c.nombre || ' ' || c.apellido, 'Consumidor Final') AS cliente,
                           COALESCE(c.documento, '') AS documento,
                           COALESCE(v.vendedor, 'Sin registrar') AS vendedor,
                           COALESCE(v.subtotal_bruto, v.total) AS subtotal_bruto,
                           COALESCE(v.porcentaje_descuento, 0) AS porcentaje_descuento,
                           COALESCE(v.monto_descuento, 0) AS monto_descuento,
                           COALESCE(v.total_final, v.total) AS total_final
                      FROM ventas v
                      LEFT JOIN clientes c ON c.id = v.id_cliente
                     WHERE 1 = 1";
                if (desde.HasValue) sql += " AND v.fecha >= @desde";
                if (hasta.HasValue) sql += " AND v.fecha < @hasta";
                if (idCliente > 0) sql += " AND v.id_cliente = @idCliente";
                sql += " ORDER BY v.fecha DESC, v.id DESC";

                using var conexion = new NpgsqlConnection(_cadenaConexion);
                conexion.Open();
                var lista = new List<VentaHistorial>();

                using (var comando = new NpgsqlCommand(sql, conexion))
                {
                    if (desde.HasValue)
                    {
                        comando.Parameters.Add(new NpgsqlParameter("desde", NpgsqlDbType.Timestamp)
                        {
                            Value = desde.Value.ToDateTime(TimeOnly.MinValue)
                        });
                    }
                    if (hasta.HasValue)
                    {
                        comando.Parameters.Add(new NpgsqlParameter("hasta", NpgsqlDbType.Timestamp)
                        {
                            Value = hasta.Value.AddDays(1).ToDateTime(TimeOnly.MinValue)
                        });
                    }
                    if (idCliente > 0) comando.Parameters.AddWithValue("idCliente", idCliente);

                    using var lector = comando.ExecuteReader();
                    while (lector.Read())
                    {
                        lista.Add(new VentaHistorial
                        {
                            Id = lector.GetInt32(0),
                            Fecha = lector.GetDateTime(1),
                            IdCliente = lector.GetInt32(2),
                            Cliente = lector.GetString(3),
                            Documento = lector.GetString(4),
                            Vendedor = lector.GetString(5),
                            SubtotalBruto = lector.GetDecimal(6),
                            PorcentajeDescuento = lector.GetDecimal(7),
                            MontoDescuento = lector.GetDecimal(8),
                            TotalFinal = lector.GetDecimal(9)
                        });
                    }
                }

                if (lista.Count == 0) return lista;

                const string sqlLineas = @"
                    SELECT d.id_venta, p.nombre, d.cantidad, d.precio_unitario, d.subtotal
                      FROM detalle_ventas d
                      JOIN productos p ON p.id = d.id_producto
                     WHERE d.id_venta = ANY(@ids)
                     ORDER BY d.id";
                var porId = lista.ToDictionary(v => v.Id);
                using (var comando = new NpgsqlCommand(sqlLineas, conexion))
                {
                    comando.Parameters.AddWithValue("ids", lista.Select(v => v.Id).ToArray());
                    using var lector = comando.ExecuteReader();
                    while (lector.Read())
                    {
                        if (!porId.TryGetValue(lector.GetInt32(0), out var venta)) continue;
                        venta.Lineas.Add(new LineaHistorial
                        {
                            Producto = lector.IsDBNull(1) ? string.Empty : lector.GetString(1),
                            Cantidad = lector.GetInt32(2),
                            PrecioUnitario = lector.GetDecimal(3),
                            Subtotal = lector.GetDecimal(4)
                        });
                    }
                }

                return lista;
            });
        }

        public List<ResumenDescuentoVendedor> ObtenerAuditoriaDescuentos()
        {
            return ProteccionBD.Ejecutar<List<ResumenDescuentoVendedor>>("Venta.ObtenerAuditoriaDescuentos", "mostrar la auditoría de descuentos", () =>
            {
                var lista = new List<ResumenDescuentoVendedor>();
                const string sql = @"
                    SELECT vendedor,
                           COUNT(*) AS ventas_con_descuento,
                           COALESCE(SUM(monto_descuento), 0) AS total_descontado
                      FROM ventas
                     WHERE porcentaje_descuento > 0
                     GROUP BY vendedor
                     ORDER BY total_descontado DESC";
                using var conexion = new NpgsqlConnection(_cadenaConexion);
                conexion.Open();
                using var comando = new NpgsqlCommand(sql, conexion);
                using var lector = comando.ExecuteReader();
                while (lector.Read())
                {
                    lista.Add(new ResumenDescuentoVendedor
                    {
                        Vendedor = lector.GetString(0),
                        VentasConDescuento = Convert.ToInt32(lector.GetInt64(1)),
                        TotalDescontado = lector.GetDecimal(2)
                    });
                }
                return lista;
            });
        }
    }
}
