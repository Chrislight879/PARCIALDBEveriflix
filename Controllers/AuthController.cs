using PARCIALDBEveriflix.Models; // Asegúrate de importar los modelos correctamente.
using System.Linq;
using System.Web.Mvc;
using System.IO;
using System.Web;
using PARCIALDBEveriflix.Helpers;

public class AuthController : Controller
{
    private EveryflixDBEntities db = new EveryflixDBEntities();

    // Acción para mostrar la página de inicio de sesión (GET)
    [HttpGet]
    public ActionResult Login()
    {
        return View();  // Debe devolver la vista Login.cshtml
    }

    // Acción para iniciar sesión (POST)
    [HttpPost]
    public ActionResult Login(string username, string password)
    {
        var user = db.Usuarios.SingleOrDefault(u => u.Username == username);
        if (user != null)
        {
            bool isPasswordValid = PasswordHelper.VerifyPassword(password, user.Password);
            if (isPasswordValid)
            {
                // Almacenar información del usuario en la sesión
                Session["Username"] = user.Username;
                Session["UserId"] = user.id_Usuario;
                Session["UserType"] = user.Id_TipoUsuario; // Guardamos el tipo de usuario

                // Redirigir a la acción correspondiente en UserController según el tipo de usuario
                return RedirectToAction("Dashboard", "User");  // Accion que manejará las vistas segun el tipo de usuario
            }
            else
            {
                // Si la contraseña es incorrecta
                ViewBag.ErrorMessage = "Contraseña incorrecta";
                return View();
            }
        }
        else
        {
            // Si el usuario no se encuentra
            ViewBag.ErrorMessage = "Usuario no encontrado";
            return View();
        }
    }

    // Acción para mostrar la página de registro (GET)
    [HttpGet]
    public ActionResult SignUp()
    {
        return View();
    }

    // Acción para registrar un nuevo usuario (POST)
    [HttpPost]
    public ActionResult SignUp(string username, string correo, string password, HttpPostedFileBase imgURL)
    {
        if (ModelState.IsValid)
        {
            string imageUrl = "/images/default.png"; // Imagen por defecto si no se sube ninguna

            if (imgURL != null && imgURL.ContentLength > 0)
            {
                // Validar la extensión del archivo de imagen
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
                var extension = Path.GetExtension(imgURL.FileName).ToLower();

                if (allowedExtensions.Contains(extension))
                {
                    var fileName = Path.GetFileName(imgURL.FileName);
                    imageUrl = "/images/" + fileName; // Almacena la imagen en la carpeta de imágenes
                    var path = Path.Combine(Server.MapPath("~/images"), fileName);
                    imgURL.SaveAs(path);
                }
                else
                {
                    ViewBag.ErrorMessage = "Solo se permiten imágenes (.jpg, .jpeg, .png, .gif)";
                    return View();
                }
            }

            var user = new Usuario
            {
                Username = username,
                Correo = correo,
                Password = PasswordHelper.HashPassword(password), // Hash de la contraseña
                Id_TipoUsuario = 1, // Administrador
                ImgURL = imageUrl // URL de la imagen
            };

            db.Usuarios.Add(user);
            db.SaveChanges();

            return RedirectToAction("Login"); // Redirige a la página de login después de registrar
        }

        return View(); // Vuelve a la vista si hay errores
    }
}