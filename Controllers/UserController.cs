using PARCIALDBEveriflix.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace PARCIALDBEveriflix.Controllers
{
    public class UserController : Controller
    {
        private EveryflixDBEntities db = new EveryflixDBEntities();

        // Acción para mostrar el Dashboard del usuario
        public ActionResult Dashboard()
        {
            if (Session["UserType"] == null || Session["Username"] == null)
            {
                return RedirectToAction("Login", "Auth"); // Si no están en sesión, redirigir al login
            }

            // Obtener el ID del usuario desde la sesión
            int userId = Convert.ToInt32(Session["UserId"]);
            var usuario = db.Usuarios.FirstOrDefault(u => u.id_Usuario == userId); // Obtener el usuario por ID

            if (usuario == null)
            {
                // Si no se encuentra el usuario, redirige a login
                return RedirectToAction("Login", "Auth");
            }

            // Pasa el usuario a la vista
            ViewBag.UserName = usuario.Username;

            // Dependiendo del tipo de usuario, redirigir a una vista diferente
            int userType = Convert.ToInt32(Session["UserType"]);
            if (userType == 1) // Administrador
            {
                return View("AdminDashboard", usuario); // Pasa el usuario al Dashboard del Administrador
            }
            else
            {
                return View("UserDashboard", usuario); // Pasa el usuario al Dashboard del Usuario
            }
        }
    }
}