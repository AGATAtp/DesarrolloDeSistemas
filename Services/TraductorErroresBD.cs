using Npgsql;

namespace PrototipoCRUD.Services
{
    /// <summary>
    /// Convierte errores técnicos de PostgreSQL en mensajes de negocio claros, respetuosos
    /// y orientados a la solución (sin SQL, sin códigos, sin culpar al usuario).
    /// </summary>
    public static class TraductorErroresBD
    {
        public const string MensajeConexion =
            "No fue posible conectar con la base de datos en este momento. Verifica que el servicio de PostgreSQL esté activo e inténtalo nuevamente; tus datos no se modificaron.";

        public const string MensajeInesperado =
            "Ocurrió un problema interno al procesar la solicitud. Tus datos no se modificaron; inténtalo de nuevo o contacta a soporte.";

        public static ErrorOperacionException ErrorConexion(Exception causa) =>
            new ErrorOperacionException(MensajeConexion, causa);

        /// <param name="accion">Verbo + objeto, ej. "guardar el cliente".</param>
        public static ErrorOperacionException Traducir(PostgresException ex, string accion)
        {
            string mensaje = ex.SqlState switch
            {
                "23505" => $"No pudimos {accion} porque ya existe un registro con esos mismos datos. Revisa que no esté repetido e inténtalo de nuevo.",
                "23503" => $"No pudimos {accion} porque está relacionado con otros registros (por ejemplo, un cliente o servicio que ya no está disponible). Actualiza la página, revisa la selección e inténtalo de nuevo.",
                "23502" => $"No pudimos {accion} porque falta un dato obligatorio. Completa todos los campos requeridos e inténtalo de nuevo.",
                "23514" => $"No pudimos {accion} porque algún valor está fuera de lo permitido (por ejemplo, un descuento mayor a 20% o una cantidad no válida). Revisa los datos e inténtalo de nuevo.",
                "22001" => $"No pudimos {accion} porque uno de los textos es demasiado largo. Acórtalo e inténtalo de nuevo.",
                "22003" or "22P02" or "22007" => $"No pudimos {accion} porque un número o una fecha no tiene el formato esperado. Revísalo e inténtalo de nuevo.",
                "40001" or "40P01" => "El sistema está atendiendo otra operación al mismo tiempo. Espera unos segundos e inténtalo de nuevo; tus datos no se modificaron.",
                "57014" => "La operación tardó más de lo esperado y se canceló. Inténtalo nuevamente; tus datos no se modificaron.",
                "57P01" or "57P02" or "57P03" or "53300" => MensajeConexion,
                "28000" or "28P01" or "3D000" => "No se pudo acceder a la base de datos con la configuración actual. Contacta a soporte técnico para revisar la conexión; tus datos no se modificaron.",
                "42P01" or "42703" => "La base de datos aún no está preparada para esta operación. Contacta a soporte técnico para completar la instalación; tus datos no se modificaron.",
                _ => $"No pudimos {accion} en este momento. Tus datos no se modificaron; inténtalo de nuevo o contacta a soporte."
            };
            return new ErrorOperacionException(mensaje, ex);
        }
    }
}
