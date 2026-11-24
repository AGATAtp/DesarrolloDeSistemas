namespace PrototipoCRUD.Services
{
    /// <summary>
    /// Falla de una operación (base de datos, red, regla de integridad) ya traducida a un
    /// mensaje comprensible para el usuario. El detalle técnico original viaja en InnerException
    /// y se guarda en errores_sistema.log; Message es SIEMPRE apto para mostrarse en pantalla.
    /// </summary>
    public class ErrorOperacionException : Exception
    {
        public ErrorOperacionException(string mensajeUsuario, Exception? causa = null)
            : base(mensajeUsuario, causa)
        {
        }
    }

    /// <summary>
    /// Cláusula de guarda: los datos recibidos no cumplen una regla de negocio.
    /// Hereda de ArgumentException; Message es apto para el usuario (sin nombre de parámetro).
    /// </summary>
    public class ValidacionNegocioException : ArgumentException
    {
        public ValidacionNegocioException(string mensajeUsuario)
            : base(mensajeUsuario)
        {
        }
    }
}
