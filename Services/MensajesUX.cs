namespace PrototipoCRUD.Services
{
    /// <summary>
    /// Red de seguridad de la capa de interfaz: decide qué texto ve el usuario.
    /// Los errores ya traducidos se muestran tal cual; cualquier otra excepción se registra
    /// en el log técnico y se reemplaza por un mensaje genérico (jamás StackTrace ni SQL).
    /// </summary>
    public static class MensajesUX
    {
        public static string Para(Exception ex, string operacion)
        {
            if (ex is ErrorOperacionException || ex is ValidacionNegocioException)
                return ex.Message;

            RegistroErrores.Registrar(ex, operacion);
            return TraductorErroresBD.MensajeInesperado;
        }
    }
}
