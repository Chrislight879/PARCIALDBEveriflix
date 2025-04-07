using PARCIALDBEveriflix.Models;
using System.Linq;
using System.Web.Mvc;
using System.IO;
using System.Web;
using PARCIALDBEveriflix.Helpers;

public class AuthController : Controller
{
    private EveryflixDBEntities db = new EveryflixDBEntities();

    // LOGIN GET
    [HttpGet]
    public ActionResult Login()
    {
        return View();
    }

    // LOGIN POST
    [HttpPost]
    public ActionResult Login(string username, string password)
    {
        var user = db.Usuarios.SingleOrDefault(u => u.Username == username);
        if (user != null)
        {
            bool isPasswordValid = PasswordHelper.VerifyPassword(password, user.Password);
            if (isPasswordValid)
            {
                Session["Username"] = user.Username;
                Session["UserId"] = user.id_Usuario;
                Session["UserType"] = user.Id_TipoUsuario;

                return RedirectToAction("Dashboard", "User");
            }
            else
            {
                ViewBag.ErrorMessage = "Contraseña incorrecta";
                return View();
            }
        }
        else
        {
            ViewBag.ErrorMessage = "Usuario no encontrado";
            return View();
        }
    }

    // SIGNUP GET
    [HttpGet]
    public ActionResult SignUp()
    {
        // ENIVAR TIPOS DE USUARIO MENOS ADMIN
        ViewBag.TiposUsuario = new SelectList(db.TipoUsuarios.Where(t => t.id_TipoUsuario != 1), "id_TipoUsuario", "Nombre");
        return View();
    }

    // SIGNUP POST
    [HttpPost]
    public ActionResult SignUp(string username, string correo, string password, int id_TipoUsuario, HttpPostedFileBase imgURL)
    {
        if (ModelState.IsValid)
        {
            // NO DEJAR REGISTRARSE COMO ADMINISTRADOR(ID = 1)
            if (id_TipoUsuario == 1)
            {
                ViewBag.ErrorMessage = "No puedes registrarte como Administrador.";
                ViewBag.TiposUsuario = new SelectList(db.TipoUsuarios.Where(t => t.id_TipoUsuario != 1), "id_TipoUsuario", "Nombre");
                return View();
            }
            // IMAGEN POR DEFECTO
            string imageUrl = "/images/profile_2_638787807624813602.jpg";

            if (imgURL != null && imgURL.ContentLength > 0)
            {
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
                var extension = Path.GetExtension(imgURL.FileName).ToLower();

                if (allowedExtensions.Contains(extension))
                {
                    var fileName = Path.GetFileName(imgURL.FileName);
                    imageUrl = "/images/" + fileName;
                    var path = Path.Combine(Server.MapPath("~/images"), fileName);
                    imgURL.SaveAs(path);
                }
                else
                {
                    ViewBag.ErrorMessage = "Solo se permiten imágenes (.jpg, .jpeg, .png, .gif)";
                    ViewBag.TiposUsuario = new SelectList(db.TipoUsuarios.Where(t => t.id_TipoUsuario != 1), "id_TipoUsuario", "Nombre");
                    return View();
                }
            }

            var user = new Usuario
            {
                Username = username,
                Correo = correo,
                Password = PasswordHelper.HashPassword(password),
                Id_TipoUsuario = id_TipoUsuario, // ASIGNAR TIPO DE USUARIO SELECCIONADO
                ImgURL = imageUrl
            };

            db.Usuarios.Add(user);
            db.SaveChanges();

            return RedirectToAction("Login");
        }
        ViewBag.TiposUsuario = new SelectList(db.TipoUsuarios.Where(t => t.id_TipoUsuario != 1), "id_TipoUsuario", "Nombre");
        return View();
    }
}
