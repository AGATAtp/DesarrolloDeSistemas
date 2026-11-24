using Microsoft.AspNetCore.Mvc;
using PrototipoCRUD.Data;
using PrototipoCRUD.Models;
using PrototipoCRUD.Services;

namespace PrototipoCRUD.Controllers
{
    public class AccountController : Controller
    {
        private readonly UsuarioDAO _usuarioDao;

        public AccountController(UsuarioDAO usuarioDao)
        {
            _usuarioDao = usuarioDao;
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            if (HttpContext.Session.GetString("UsuarioNombre") != null)
                return RedirectToAction("Index", "Home");
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Login(string nombreUsuario, string contrasena, string? returnUrl = null)
        {
            // Cláusula de guarda: campos vacíos
            if (string.IsNullOrWhiteSpace(nombreUsuario) || string.IsNullOrEmpty(contrasena))
            {
                ViewBag.Error = "Escribe tu usuario y tu contraseña para ingresar.";
                ViewBag.ReturnUrl = returnUrl;
                ViewBag.NombreUsuario = nombreUsuario;
                return View();
            }

            ResultadoLogin resultado;
            Usuario? usuario;
            try
            {
                var servicio = new ServicioAutenticacion(_usuarioDao);
                (resultado, usuario) = servicio.Autenticar(nombreUsuario.Trim(), contrasena);
            }
            catch (ValidacionNegocioException exValidacion)
            {
                ViewBag.Error = exValidacion.Message;
                ViewBag.ReturnUrl = returnUrl;
                ViewBag.NombreUsuario = nombreUsuario;
                return View();
            }
            catch (ErrorOperacionException exNegocio)   // BD apagada, etc. (ya traducido)
            {
                ViewBag.Error = exNegocio.Message;
                ViewBag.ReturnUrl = returnUrl;
                ViewBag.NombreUsuario = nombreUsuario;
                return View();
            }
            catch (Exception ex)
            {
                RegistroErrores.Registrar(ex, "AccountController.Login");
                ViewBag.Error = TraductorErroresBD.MensajeInesperado;
                ViewBag.ReturnUrl = returnUrl;
                ViewBag.NombreUsuario = nombreUsuario;
                return View();
            }

            switch (resultado)
            {
                case ResultadoLogin.Exito:
                    HttpContext.Session.SetInt32("UsuarioId", usuario!.Id);
                    HttpContext.Session.SetInt32("EmpleadoId", usuario.EmpleadoId);
                    HttpContext.Session.SetString("UsuarioNombre", usuario.NombreUsuario);
                    HttpContext.Session.SetString("NombreCompleto", usuario.NombreCompleto);
                    HttpContext.Session.SetString("Rol", usuario.Rol);
                    if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                        return Redirect(returnUrl);
                    return RedirectToAction("Index", "Home");
                case ResultadoLogin.Bloqueado:
                    ViewBag.Error = "Tu cuenta está bloqueada por intentos fallidos. Contacta al administrador.";
                    break;
                case ResultadoLogin.Inactivo:
                    ViewBag.Error = "Tu cuenta está inactiva. Contacta al administrador.";
                    break;
                default:
                    ViewBag.Error = "Usuario o contraseña incorrectos. Inténtalo de nuevo.";
                    break;
            }

            ViewBag.ReturnUrl = returnUrl;
            ViewBag.NombreUsuario = nombreUsuario;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }
    }
}
