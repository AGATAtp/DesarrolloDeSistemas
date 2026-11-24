using Npgsql;
using PrototipoCRUD.Services;

namespace PrototipoCRUD.Data
{
    /// <summary>
    /// Envoltorio con el patrón try-catch ordenado (PostgresException → NpgsqlException → Exception)
    /// para las consultas de apoyo. Registra el detalle técnico, traduce a mensaje UX y
    /// relanza con throw; (sin ex) cuando no sabe qué hacer, preservando el Stack Trace.
    /// </summary>
    public static class ProteccionBD
    {
        public static T Ejecutar<T>(string operacion, string accion, Func<T> trabajo)
        {
            try
            {
                return trabajo();
            }
            catch (PostgresException exPg)
            {
                RegistroErrores.Registrar(exPg, operacion);
                throw TraductorErroresBD.Traducir(exPg, accion);
            }
            catch (NpgsqlException exRed)
            {
                RegistroErrores.Registrar(exRed, operacion);
                throw TraductorErroresBD.ErrorConexion(exRed);
            }
            catch (Exception ex) when (ex is not ErrorOperacionException && ex is not ValidacionNegocioException)
            {
                RegistroErrores.Registrar(ex, operacion);
                throw;
            }
        }

        public static void Ejecutar(string operacion, string accion, Action trabajo)
        {
            Ejecutar<object?>(operacion, accion, () =>
            {
                trabajo();
                return null;
            });
        }
    }
}
