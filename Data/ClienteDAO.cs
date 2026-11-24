using Npgsql;
using PrototipoCRUD.Models;
using PrototipoCRUD.Services;

namespace PrototipoCRUD.Data
{
    public class ClienteDAO
    {
        private readonly string _cadenaConexion;

        private const string Columnas =
            "id, documento, nombre, apellido, telefono, correo, direccion, activo, tipo_cliente, fecha_registro";

        public ClienteDAO(IConfiguration configuracion)
        {
            _cadenaConexion = configuracion.GetConnectionString("Pepokita")
                ?? throw new InvalidOperationException("Falta la cadena de conexión 'Pepokita'.");
        }

        // Crea la tabla si no existe y agrega las columnas nuevas (tipo y fecha de registro)
        public void AsegurarEstructura()
        {
            const string sql = @"
                CREATE TABLE IF NOT EXISTS clientes (
                    id        SERIAL PRIMARY KEY,
                    documento VARCHAR(20)  NOT NULL UNIQUE,
                    nombre    VARCHAR(80)  NOT NULL,
                    apellido  VARCHAR(80)  NOT NULL,
                    telefono  VARCHAR(20)  NOT NULL,
                    correo    VARCHAR(120),
                    direccion TEXT,
                    activo    BOOLEAN      NOT NULL DEFAULT TRUE
                );
                ALTER TABLE clientes ADD COLUMN IF NOT EXISTS tipo_cliente VARCHAR(20) NOT NULL DEFAULT 'Particular';
                ALTER TABLE clientes ADD COLUMN IF NOT EXISTS fecha_registro DATE NOT NULL DEFAULT CURRENT_DATE;";
            using var conexion = new NpgsqlConnection(_cadenaConexion);
            conexion.Open();
            using var comando = new NpgsqlCommand(sql, conexion);
            comando.ExecuteNonQuery();
        }

        // ---------- Cláusulas de guarda (validación previa, antes de tocar la BD) ----------
        private static void ValidarCliente(Cliente? c)
        {
            if (c == null)
                throw new ValidacionNegocioException("No recibimos los datos del cliente. Completa el formulario e inténtalo de nuevo.");
            if (string.IsNullOrWhiteSpace(c.Nombre) || string.IsNullOrWhiteSpace(c.Apellido))
                throw new ValidacionNegocioException("El nombre y el apellido del cliente son obligatorios. Complétalos para continuar.");
            if (string.IsNullOrWhiteSpace(c.Documento))
                throw new ValidacionNegocioException("El documento de identidad es obligatorio. Escríbelo para continuar.");
            if (string.IsNullOrWhiteSpace(c.Telefono))
                throw new ValidacionNegocioException("El teléfono es obligatorio para avisar cuando el vehículo esté listo.");
        }

        // CREATE
        public void Insertar(Cliente c)
        {
            ValidarCliente(c);   // guarda

            const string sql = @"INSERT INTO clientes
                                    (documento, nombre, apellido, telefono, correo, direccion, tipo_cliente, fecha_registro)
                                 VALUES
                                    (@documento, @nombre, @apellido, @telefono, @correo, @direccion, @tipo, @fecha)";
            NpgsqlConnection? conexion = null;
            try
            {
                conexion = new NpgsqlConnection(_cadenaConexion);
                conexion.Open();
                using var comando = new NpgsqlCommand(sql, conexion);
                AgregarParametros(comando, c);
                comando.ExecuteNonQuery();
            }
            catch (PostgresException exPg)   // más específica: error del motor de BD
            {
                RegistroErrores.Registrar(exPg, "Cliente.Insertar");
                if (exPg.SqlState == "23505")   // UNIQUE: documento repetido
                    throw new ErrorOperacionException(
                        $"El documento '{c.Documento}' ya pertenece a otro cliente. Búscalo en la lista o ingresa un documento distinto.", exPg);
                throw TraductorErroresBD.Traducir(exPg, "guardar el cliente");
            }
            catch (NpgsqlException exRed)    // conexión / servidor apagado
            {
                RegistroErrores.Registrar(exRed, "Cliente.Insertar");
                throw TraductorErroresBD.ErrorConexion(exRed);
            }
            catch (Exception ex)             // más general, al final
            {
                RegistroErrores.Registrar(ex, "Cliente.Insertar");
                throw;                       // preserva el Stack Trace
            }
            finally
            {
                conexion?.Dispose();         // cierra la conexión ocurra o no un error
            }
        }

        // READ (lista de clientes activos)
        public List<Cliente> ObtenerTodos()
        {
            var lista = new List<Cliente>();
            var sql = $"SELECT {Columnas} FROM clientes WHERE activo = TRUE ORDER BY apellido, nombre";
            NpgsqlConnection? conexion = null;
            try
            {
                conexion = new NpgsqlConnection(_cadenaConexion);
                conexion.Open();
                using var comando = new NpgsqlCommand(sql, conexion);
                using var lector = comando.ExecuteReader();
                while (lector.Read())
                {
                    lista.Add(Mapear(lector));
                }
                return lista;
            }
            catch (PostgresException exPg)
            {
                RegistroErrores.Registrar(exPg, "Cliente.ObtenerTodos");
                throw TraductorErroresBD.Traducir(exPg, "mostrar la lista de clientes");
            }
            catch (NpgsqlException exRed)
            {
                RegistroErrores.Registrar(exRed, "Cliente.ObtenerTodos");
                throw TraductorErroresBD.ErrorConexion(exRed);
            }
            catch (Exception ex)
            {
                RegistroErrores.Registrar(ex, "Cliente.ObtenerTodos");
                throw;
            }
            finally
            {
                conexion?.Dispose();
            }
        }

        // READ (uno por id)
        public Cliente? ObtenerPorId(int id)
        {
            if (id <= 0)
                throw new ValidacionNegocioException("Selecciona un cliente de la lista para continuar.");

            return ProteccionBD.Ejecutar<Cliente?>("Cliente.ObtenerPorId", "consultar el cliente", () =>
            {
                var sql = $"SELECT {Columnas} FROM clientes WHERE id = @id AND activo = TRUE";
                using var conexion = new NpgsqlConnection(_cadenaConexion);
                conexion.Open();
                using var comando = new NpgsqlCommand(sql, conexion);
                comando.Parameters.AddWithValue("id", id);
                using var lector = comando.ExecuteReader();
                return lector.Read() ? Mapear(lector) : null;
            });
        }

        // UPDATE
        public void Actualizar(Cliente c)
        {
            ValidarCliente(c);   // guarda
            if (c.Id <= 0)
                throw new ValidacionNegocioException("Selecciona el cliente que deseas editar antes de guardar los cambios.");

            const string sql = @"UPDATE clientes
                                 SET documento = @documento, nombre = @nombre, apellido = @apellido,
                                     telefono = @telefono, correo = @correo, direccion = @direccion,
                                     tipo_cliente = @tipo, fecha_registro = @fecha
                                 WHERE id = @id";
            NpgsqlConnection? conexion = null;
            try
            {
                conexion = new NpgsqlConnection(_cadenaConexion);
                conexion.Open();
                using var comando = new NpgsqlCommand(sql, conexion);
                AgregarParametros(comando, c);
                comando.Parameters.AddWithValue("id", c.Id);
                if (comando.ExecuteNonQuery() == 0)
                    throw new ErrorOperacionException(
                        "No encontramos al cliente que intentas editar; es posible que otra persona lo haya eliminado. Actualiza la lista e inténtalo de nuevo.");
            }
            catch (PostgresException exPg)
            {
                RegistroErrores.Registrar(exPg, "Cliente.Actualizar");
                if (exPg.SqlState == "23505")
                    throw new ErrorOperacionException(
                        $"El documento '{c.Documento}' ya pertenece a otro cliente. Ingresa un documento distinto.", exPg);
                throw TraductorErroresBD.Traducir(exPg, "actualizar el cliente");
            }
            catch (NpgsqlException exRed)
            {
                RegistroErrores.Registrar(exRed, "Cliente.Actualizar");
                throw TraductorErroresBD.ErrorConexion(exRed);
            }
            catch (Exception ex) when (ex is not ErrorOperacionException)
            {
                RegistroErrores.Registrar(ex, "Cliente.Actualizar");
                throw;
            }
            finally
            {
                conexion?.Dispose();
            }
        }

        // DELETE (baja lógica: se conserva el historial del cliente)
        public void Eliminar(int id)
        {
            if (id <= 0)
                throw new ValidacionNegocioException("Selecciona un cliente de la lista antes de darlo de baja.");

            const string sql = "UPDATE clientes SET activo = FALSE WHERE id = @id";
            NpgsqlConnection? conexion = null;
            try
            {
                conexion = new NpgsqlConnection(_cadenaConexion);
                conexion.Open();
                using var comando = new NpgsqlCommand(sql, conexion);
                comando.Parameters.AddWithValue("id", id);
                if (comando.ExecuteNonQuery() == 0)
                    throw new ErrorOperacionException(
                        "No encontramos al cliente seleccionado; es posible que ya haya sido dado de baja. Actualiza la lista para ver los datos más recientes.");
            }
            catch (PostgresException exPg)
            {
                RegistroErrores.Registrar(exPg, "Cliente.Eliminar");
                throw TraductorErroresBD.Traducir(exPg, "dar de baja al cliente");
            }
            catch (NpgsqlException exRed)
            {
                RegistroErrores.Registrar(exRed, "Cliente.Eliminar");
                throw TraductorErroresBD.ErrorConexion(exRed);
            }
            catch (Exception ex) when (ex is not ErrorOperacionException)
            {
                RegistroErrores.Registrar(ex, "Cliente.Eliminar");
                throw;
            }
            finally
            {
                conexion?.Dispose();
            }
        }

        // Validación: el documento es único (incluye clientes dados de baja)
        public bool ExisteDocumento(string documento, int excluirId = 0)
        {
            if (string.IsNullOrWhiteSpace(documento))
                throw new ValidacionNegocioException("Escribe el documento de identidad para continuar.");

            return ProteccionBD.Ejecutar<bool>("Cliente.ExisteDocumento", "verificar el documento", () =>
            {
                const string sql = "SELECT COUNT(*) FROM clientes WHERE documento = @documento AND id <> @id";
                using var conexion = new NpgsqlConnection(_cadenaConexion);
                conexion.Open();
                using var comando = new NpgsqlCommand(sql, conexion);
                comando.Parameters.AddWithValue("documento", documento);
                comando.Parameters.AddWithValue("id", excluirId);
                return Convert.ToInt64(comando.ExecuteScalar()) > 0;
            });
        }

        // Datos para el Panel principal
        public (int Activos, int NuevosMes) Resumen()
        {
            return ProteccionBD.Ejecutar<(int Activos, int NuevosMes)>("Cliente.Resumen", "mostrar el resumen de clientes", () =>
            {
                const string sql = @"SELECT
                                        COUNT(*) FILTER (WHERE activo),
                                        COUNT(*) FILTER (WHERE activo
                                            AND date_trunc('month', fecha_registro) = date_trunc('month', CURRENT_DATE))
                                     FROM clientes";
                using var conexion = new NpgsqlConnection(_cadenaConexion);
                conexion.Open();
                using var comando = new NpgsqlCommand(sql, conexion);
                using var lector = comando.ExecuteReader();
                lector.Read();
                return ((int)lector.GetInt64(0), (int)lector.GetInt64(1));
            });
        }

        private static void AgregarParametros(NpgsqlCommand comando, Cliente c)
        {
            comando.Parameters.AddWithValue("documento", c.Documento);
            comando.Parameters.AddWithValue("nombre", c.Nombre);
            comando.Parameters.AddWithValue("apellido", c.Apellido);
            comando.Parameters.AddWithValue("telefono", c.Telefono);
            comando.Parameters.AddWithValue("correo",
                string.IsNullOrWhiteSpace(c.Correo) ? DBNull.Value : c.Correo);
            comando.Parameters.AddWithValue("direccion",
                string.IsNullOrWhiteSpace(c.Direccion) ? DBNull.Value : c.Direccion);
            comando.Parameters.AddWithValue("tipo", c.TipoCliente);
            comando.Parameters.AddWithValue("fecha", c.FechaRegistro);
        }

        private static Cliente Mapear(NpgsqlDataReader r) => new Cliente
        {
            Id = r.GetInt32(0),
            Documento = r.GetString(1),
            Nombre = r.GetString(2),
            Apellido = r.GetString(3),
            Telefono = r.GetString(4),
            Correo = r.IsDBNull(5) ? null : r.GetString(5),
            Direccion = r.IsDBNull(6) ? null : r.GetString(6),
            Activo = r.GetBoolean(7),
            TipoCliente = r.GetString(8),
            FechaRegistro = r.GetFieldValue<DateOnly>(9)
        };
    }
}
