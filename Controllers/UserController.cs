using PARCIALDBEveriflix.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Data.Entity;
using System.Web.Configuration;

namespace PARCIALDBEveriflix.Controllers
{
    public class UserController : Controller
    {
        private EveryflixDBEntities db = new EveryflixDBEntities();

        // Método seguro para verificar el tipo de usuario
        private bool IsAdminUser()
        {
            if (Session["UserType"] == null)
            {
                return false;
            }

            try
            {
                int userType;
                if (Session["UserType"] is int)
                {
                    userType = (int)Session["UserType"];
                }
                else if (int.TryParse(Session["UserType"].ToString(), out userType))
                {
                    // Conversión exitosa
                }
                else
                {
                    return false;
                }

                return userType == 1; // 1 = Administrador
            }
            catch
            {
                return false;
            }
        }
       
        public ActionResult EditUser(long id)
        {
            // Verificar si el usuario actual es el administrador
            var usuario = db.Usuarios.FirstOrDefault(u => u.id_Usuario == id);
            if (usuario == null)
            {
                return HttpNotFound();
            }

            // Cargar los tipos de usuario disponibles (si es necesario)
            ViewBag.TiposUsuario = new SelectList(db.TipoUsuarios, "Id_TipoUsuario", "Nombre");

            return View(usuario);
        }

        [HttpPost]
    
        [ValidateAntiForgeryToken]
        public ActionResult EditUser(Usuario usuario)
        {
            if (ModelState.IsValid)
            {
                // Encontrar el usuario que se está editando
                var existingUser = db.Usuarios.FirstOrDefault(u => u.id_Usuario == usuario.id_Usuario);

                if (existingUser != null)
                {
                    // Solo el administrador puede cambiar el tipo de usuario
                    existingUser.Id_TipoUsuario = usuario.Id_TipoUsuario;

                    // Guardar los cambios en la base de datos
                    db.SaveChanges();
                    return RedirectToAction("ManageUsers");// O cualquier otra vista a la que redirijas
                }
                else
                {
                    return HttpNotFound();
                }
            }

            // Si la validación del modelo falla, vuelve a mostrar el formulario
            ViewBag.TiposUsuario = new SelectList(db.TipoUsuarios, "Id_TipoUsuario", "Nombre", usuario.Id_TipoUsuario);
            return View(usuario);
        }

        // Acción para editar perfil

        [HttpGet]
        public ActionResult EditProfile(int? id)
        {
            if (Session["UserId"] == null)
            {
                return RedirectToAction("Login", "Auth"); // Si no está logueado, redirigir al login
            }

            // Si el id es null, usamos el id del usuario logueado
            int userId = id ?? Convert.ToInt32(Session["UserId"]);

            var usuario = db.Usuarios.FirstOrDefault(u => u.id_Usuario == userId);
            if (usuario == null)
            {
                return RedirectToAction("Login", "Auth");
            }

            // Si es un admin, puede editar cualquier perfil
            if (IsAdminUser())
            {
                ViewBag.IsAdmin = true;
            }

            return View(usuario);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditProfile(Usuario updatedUser, HttpPostedFileBase ImgURL)
        {
            if (Session["UserId"] == null)
            {
                return RedirectToAction("Login", "Auth");
            }

            try
            {
                int userId = Convert.ToInt32(Session["UserId"]);
                var currentUser = db.Usuarios.FirstOrDefault(u => u.id_Usuario == userId);

                if (currentUser == null)
                {
                    return RedirectToAction("Login", "Auth");
                }

                // Determinar qué usuario estamos editando
                Usuario usuarioAEditar;
                if (IsAdminUser() && updatedUser.id_Usuario != userId)
                {
                    usuarioAEditar = db.Usuarios.Find(updatedUser.id_Usuario);
                    if (usuarioAEditar == null)
                    {
                        TempData["ErrorMessage"] = "Usuario no encontrado";
                        return RedirectToAction("ManageUsers");
                    }
                }
                else
                {
                    usuarioAEditar = currentUser;
                }

                // Validar modelo
                if (string.IsNullOrWhiteSpace(updatedUser.Username))
                {
                    ModelState.AddModelError("Username", "El nombre de usuario no puede estar vacío");
                    return View(usuarioAEditar);
                }

                // Verificar si el username ya existe
                if (db.Usuarios.Any(u => u.Username == updatedUser.Username && u.id_Usuario != usuarioAEditar.id_Usuario))
                {
                    ModelState.AddModelError("Username", "Este nombre de usuario ya está en uso");
                    return View(usuarioAEditar);
                }

                // Actualizar username
                usuarioAEditar.Username = updatedUser.Username;

                // Manejo de imagen
                if (ImgURL != null && ImgURL.ContentLength > 0)
                {
                    var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
                    var extension = Path.GetExtension(ImgURL.FileName).ToLower();

                    if (!allowedExtensions.Contains(extension))
                    {
                        ModelState.AddModelError("ImgURL", "Solo se permiten imágenes (.jpg, .jpeg, .png, .gif)");
                        return View(usuarioAEditar);
                    }

                    // Crear directorio si no existe
                    var imagesPath = Server.MapPath("~/images");
                    if (!Directory.Exists(imagesPath))
                    {
                        Directory.CreateDirectory(imagesPath);
                    }

                    var fileName = $"profile_{usuarioAEditar.id_Usuario}_{DateTime.Now.Ticks}{extension}";
                    var imageUrl = "/images/" + fileName;
                    var path = Path.Combine(imagesPath, fileName);

                    // Eliminar imagen anterior si existe
                    if (!string.IsNullOrEmpty(usuarioAEditar.ImgURL))
                    {
                        var oldPath = Server.MapPath(usuarioAEditar.ImgURL);
                        if (System.IO.File.Exists(oldPath))
                        {
                            System.IO.File.Delete(oldPath);
                        }
                    }

                    ImgURL.SaveAs(path);
                    usuarioAEditar.ImgURL = imageUrl;
                }

                // Guardar cambios
                db.Entry(usuarioAEditar).State = EntityState.Modified;
                int changes = db.SaveChanges();

                if (changes > 0)
                {
                    // Actualizar sesión si es el usuario actual
                    if (usuarioAEditar.id_Usuario == userId)
                    {
                        Session["Username"] = usuarioAEditar.Username;
                    }
                    TempData["SuccessMessage"] = "Perfil actualizado correctamente";
                }
                else
                {
                    TempData["WarningMessage"] = "No se realizaron cambios";
                }

                return RedirectToAction("Dashboard");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al editar perfil: {ex.Message}");
                TempData["ErrorMessage"] = "Ocurrió un error al guardar los cambios. Por favor intente nuevamente.";
                return View(db.Usuarios.Find(Convert.ToInt32(Session["UserId"])));
            }
        }
        // Acción para mostrar el Dashboard del usuario
        public ActionResult Dashboard()
        {
            if (Session["UserType"] == null || Session["Username"] == null)
            {
                return RedirectToAction("Login", "Auth"); // Si no están en sesión, redirigir al login
            }

            // Obtener el ID del usuario desde la sesión
            int userId = Convert.ToInt32(Session["UserId"]);
            var usuario = db.Usuarios.FirstOrDefault(u => u.id_Usuario == userId);

            if (usuario == null)
            {
                return RedirectToAction("Login", "Auth"); // Si no se encuentra el usuario, redirigir a login
            }

            ViewBag.UserName = usuario.Username;

            int userType = Convert.ToInt32(Session["UserType"]);

            switch (userType)
            {
                case 1: // Administrador
                    ViewBag.FollowerCount = db.Seguidors.Count(s => s.Id_UsuarioSiguiendo == userId);
                    ViewBag.FollowingCount = db.Seguidors.Count(s => s.Id_UsuarioSeguidor == userId);
                    return View("AdminDashboard", usuario); // Redirigir al Dashboard del Administrador

                case 2: // Artista
                    var artista = db.Artistas.FirstOrDefault(a => a.Id_Usuario == userId);
                    if (artista != null)
                    {
                        ViewBag.FollowerCount = db.Seguidors.Count(s => s.Id_UsuarioSiguiendo == userId);
                        ViewBag.FollowingCount = db.Seguidors.Count(s => s.Id_UsuarioSeguidor == userId);
                        ViewBag.ArtistaInfo = artista;
                    }
                    // Obtener lista de usuarios que sigue el usuario
                    ViewBag.FollowingList = db.Seguidors
                                              .Where(s => s.Id_UsuarioSeguidor == userId)
                                              .Select(s => s.Usuario1) // Obtenemos la información del usuario seguido
                                              .ToList();
                    return View("ArtistDashboard", usuario); // Redirigir al Dashboard del Artista

                case 3: // Usuario normal
                    ViewBag.FollowerCount = db.Seguidors.Count(s => s.Id_UsuarioSiguiendo == userId);
                    ViewBag.FollowingCount = db.Seguidors.Count(s => s.Id_UsuarioSeguidor == userId);
                    // Obtener lista de usuarios que sigue el usuario
                    ViewBag.FollowingList = db.Seguidors
                                              .Where(s => s.Id_UsuarioSeguidor == userId)
                                              .Select(s => s.Usuario1) // Obtenemos la información del usuario seguido
                                              .ToList();
                    return View("UserDashboard", usuario); // Redirigir al Dashboard del Usuario

                case 4: // Disquera
                    var disquera = db.Disqueras.FirstOrDefault(d => d.Id_Usuario == userId);
                    if (disquera != null)
                    {
                        ViewBag.FollowerCount = db.Seguidors.Count(s => s.Id_UsuarioSiguiendo == userId);
                        ViewBag.FollowingCount = db.Seguidors.Count(s => s.Id_UsuarioSeguidor == userId);
                        ViewBag.DisqueraInfo = disquera;
                    }
                    // Obtener lista de usuarios que sigue el usuario
                    ViewBag.FollowingList = db.Seguidors
                                              .Where(s => s.Id_UsuarioSeguidor == userId)
                                              .Select(s => s.Usuario1) // Obtenemos la información del usuario seguido
                                              .ToList();
                    return View("DisqueraDashboard", usuario); // Redirigir al Dashboard de la Disquera

                case 5: // Productor
                    var productor = db.Productors.FirstOrDefault(p => p.Id_Usuario == userId);
                    if (productor != null)
                    {
                        ViewBag.FollowerCount = db.Seguidors.Count(s => s.Id_UsuarioSiguiendo == userId);
                        ViewBag.FollowingCount = db.Seguidors.Count(s => s.Id_UsuarioSeguidor == userId);
                        ViewBag.ProductorInfo = productor;
                    }
                    // Obtener lista de usuarios que sigue el usuario
                    ViewBag.FollowingList = db.Seguidors
                                              .Where(s => s.Id_UsuarioSeguidor == userId)
                                              .Select(s => s.Usuario1) // Obtenemos la información del usuario seguido
                                              .ToList();
                    return View("ProductorDashboard", usuario); // Redirigir al Dashboard del Productor

                case 6: // Director
                    var director = db.Directors.FirstOrDefault(d => d.Id_Usuario == userId);
                    if (director != null)
                    {
                        ViewBag.FollowerCount = db.Seguidors.Count(s => s.Id_UsuarioSiguiendo == userId);
                        ViewBag.FollowingCount = db.Seguidors.Count(s => s.Id_UsuarioSeguidor == userId);
                        ViewBag.DirectorInfo = director;
                    }
                    // Obtener lista de usuarios que sigue el usuario
                    ViewBag.FollowingList = db.Seguidors
                                              .Where(s => s.Id_UsuarioSeguidor == userId)
                                              .Select(s => s.Usuario1) // Obtenemos la información del usuario seguido
                                              .ToList();
                    return View("DirectorDashboard", usuario); // Redirigir al Dashboard del Director

                default:
                    ViewBag.ErrorMessage = "Tipo de usuario desconocido.";
                    return RedirectToAction("Login", "Auth");
            }
        }


        // Acción para gestionar usuarios (ver lista de usuarios)
        public ActionResult ManageUsers()
        {
            if (!IsAdminUser())  // Verifica si el usuario tiene privilegios de administrador
            {
                TempData["ErrorMessage"] = "Acceso denegado: Se requieren privilegios de administrador";
                return RedirectToAction("Login", "Auth");
            }

            var usuarios = db.Usuarios.Include(u => u.TipoUsuario).ToList(); // Asegúrate de que la relación esté bien cargada
            return View(usuarios);  // Devuelve la lista de usuarios para la vista
        }
        // Método para seguir a un usuario
        [HttpPost]
        public ActionResult FollowUser(int userIdToFollow)
        {
            if (Session["UserId"] == null)
            {
                return RedirectToAction("Login", "Auth");
            }

            int followerId = Convert.ToInt32(Session["UserId"]);

            // Evitar que un usuario se siga a sí mismo
            if (followerId == userIdToFollow)
            {
                TempData["ErrorMessage"] = "No puedes seguirte a ti mismo";
                return RedirectToAction("SearchUsers");
            }

            // Verificar si ya sigue al usuario
            var existingFollow = db.Seguidors.FirstOrDefault(s =>
                s.Id_UsuarioSeguidor == followerId &&
                s.Id_UsuarioSiguiendo == userIdToFollow);

            if (existingFollow == null)
            {
                var newFollow = new Seguidor
                {
                    Id_UsuarioSeguidor = followerId,
                    Id_UsuarioSiguiendo = userIdToFollow
                };

                db.Seguidors.Add(newFollow);
                db.SaveChanges();
                TempData["SuccessMessage"] = "Ahora sigues a este usuario";
            }
            else
            {
                TempData["ErrorMessage"] = "Ya sigues a este usuario";
            }

            return RedirectToAction("SearchUsers");
        }

        // Método para dejar de seguir
        [HttpPost]
        public ActionResult UnfollowUser(int userIdToUnfollow)
        {
            if (Session["UserId"] == null)
            {
                return RedirectToAction("Login", "Auth");
            }

            int followerId = Convert.ToInt32(Session["UserId"]);

            var followToRemove = db.Seguidors.FirstOrDefault(s =>
                s.Id_UsuarioSeguidor == followerId &&
                s.Id_UsuarioSiguiendo == userIdToUnfollow);

            if (followToRemove != null)
            {
                db.Seguidors.Remove(followToRemove);
                db.SaveChanges();
                TempData["SuccessMessage"] = "Has dejado de seguir a este usuario"; // Mensaje de éxito
            }
            else
            {
                TempData["ErrorMessage"] = "No estabas siguiendo a este usuario"; // Mensaje de error si no estabas siguiendo
            }

            // Redirigir de nuevo a la vista de búsqueda de usuarios
            return RedirectToAction("SearchUsers"); // Redirige a SearchUsers en lugar de FollowingList
        }


        // Vista para buscar usuarios
        public ActionResult SearchUsers(string searchString)
        {
            if (Session["UserId"] == null)
            {
                return RedirectToAction("Login", "Auth");
            }

            var users = db.Usuarios.AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                users = users.Where(u => u.Username.Contains(searchString));
            }

            int currentUserId = Convert.ToInt32(Session["UserId"]);
            ViewBag.CurrentUserId = currentUserId;

            // Obtener los IDs de usuarios que ya sigue
            var followingIds = db.Seguidors
                .Where(s => s.Id_UsuarioSeguidor == currentUserId)
                .Select(s => s.Id_UsuarioSiguiendo)
                .ToList();

            ViewBag.FollowingIds = followingIds;

            return View(users.ToList());
        }

        // Vista para mostrar seguidores
        public ActionResult FollowersList()
        {
            if (Session["UserId"] == null)
            {
                return RedirectToAction("Login", "Auth");
            }

            int userId = Convert.ToInt32(Session["UserId"]);

            var followers = db.Seguidors
                .Where(s => s.Id_UsuarioSiguiendo == userId)
                .Select(s => s.Usuario) // Usuario que sigue (seguidor)
                .ToList();

            return View(followers);
        }

        // Vista para mostrar a quién sigue
        public ActionResult FollowingList()
        {
            if (Session["UserId"] == null)
            {
                return RedirectToAction("Login", "Auth");
            }

            int userId = Convert.ToInt32(Session["UserId"]);

            var following = db.Seguidors
                .Where(s => s.Id_UsuarioSeguidor == userId)
                .Select(s => s.Usuario1) // Usuario seguido
                .ToList();

            return View(following);
        }
        // Accion GET para mostrar una confirmación de eliminación
        public ActionResult DeleteUser(long id)
        {
            var usuarioAEliminar = db.Usuarios.Find(id);
            if (usuarioAEliminar == null)
            {
                TempData["ErrorMessage"] = "Usuario no encontrado.";
                return RedirectToAction("ManageUsers");
            }

            return View(usuarioAEliminar);  // Puedes mostrar una vista de confirmación
        }

        [HttpPost, ActionName("DeleteUser")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(long id)
        {
            var usuarioAEliminar = db.Usuarios.Find(id);
            if (usuarioAEliminar == null)
            {
                TempData["ErrorMessage"] = "Usuario no encontrado.";
                return RedirectToAction("ManageUsers");
            }

            // Eliminar los seguidores relacionados con el usuario
            var seguidores = db.Seguidors.Where(s => s.Id_UsuarioSeguidor == id).ToList();
            db.Seguidors.RemoveRange(seguidores);
                
            // Ahora eliminar el usuario
            db.Usuarios.Remove(usuarioAEliminar);
            db.SaveChanges();

            TempData["SuccessMessage"] = "Usuario eliminado correctamente.";
            return RedirectToAction("ManageUsers");
        }

    }

}
