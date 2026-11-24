using System.Text;
using Npgsql;

namespace PrototipoCRUD.Services
{
    /// <summary>
    /// Log técnico para el desarrollador. Escribe en errores_sistema.log (carpeta del proyecto)
    /// y en la consola (Console.Error). Nunca lanza excepciones: registrar un error no debe
    /// provocar otro error.
    /// </summary>
    public static class RegistroErrores
    {
        private static readonly object Candado = new();
        private const string ClaveRegistrado = "PrototipoCRUD.YaRegistrado";

        public static string RutaArchivo { get; } =
            Path.Combine(Directory.GetCurrentDirectory(), "errores_sistema.log");

        public static void Registrar(Exception? ex, string operacion = "")
        {
            if (ex == null) return;

            // Evita duplicar la misma excepción cuando se relanza (throw;) entre capas
            if (ex.Data.Contains(ClaveRegistrado)) return;
            if (!ex.Data.IsReadOnly) ex.Data[ClaveRegistrado] = true;

            var texto = new StringBuilder();
            texto.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Operación: {operacion}");
            texto.AppendLine($"{ex.GetType().Name}: {ex.Message}");

            if (ex is PostgresException pg)
            {
                texto.AppendLine($"SqlState={pg.SqlState} Tabla={pg.TableName} Restricción={pg.ConstraintName}");
                if (!string.IsNullOrWhiteSpace(pg.Detail)) texto.AppendLine($"Detalle={pg.Detail}");
            }

            texto.AppendLine(ex.ToString());   // incluye StackTrace y excepciones internas
            texto.AppendLine("---");

            var contenido = texto.ToString();
            Console.Error.WriteLine(contenido);

            lock (Candado)
            {
                try
                {
                    File.AppendAllText(RutaArchivo, contenido, Encoding.UTF8);
                }
                catch (Exception exLog)
                {
                    Console.Error.WriteLine($"No se pudo escribir en {RutaArchivo}: {exLog.Message}");
                }
            }
        }
    }
}
