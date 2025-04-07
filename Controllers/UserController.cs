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

        // METODO PARA VERIFICAR UN ADMIN
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
                    // CONVERSION DEL USUARIO ID
                }
                else
                {
                    return false;
                }

                return userType == 1; // 1 = ADMIN
            }
            catch
            {
                return false;
            }
        }

        public ActionResult EditUser(long id)
        {
            // VERIFICACION SI ES UN ADMIN
            var usuario = db.Usuarios.FirstOrDefault(u => u.id_Usuario == id);
            if (usuario == null)
            {
                return HttpNotFound();
            }

            // CARGAR TODOS LOS USUARIOS DE TODOS TIPOS
            ViewBag.TiposUsuario = new SelectList(db.TipoUsuarios, "Id_TipoUsuario", "Nombre");

            return View(usuario);
        }

        [HttpPost]

        [ValidateAntiForgeryToken]
        public ActionResult EditUser(Usuario usuario)
        {
            if (ModelState.IsValid)
            {
                // LLEVAR EL ID DE USUARIO A EDITAR
                var existingUser = db.Usuarios.FirstOrDefault(u => u.id_Usuario == usuario.id_Usuario);

                if (existingUser != null)
                {
                    existingUser.Id_TipoUsuario = usuario.Id_TipoUsuario;

                    // GUARDAR
                    db.SaveChanges();
                    return RedirectToAction("ManageUsers");
                }
                else
                {
                    return HttpNotFound();
                }
            }

            // EN CASO DE FALLO MOSTRAR FORMULARIO
            ViewBag.TiposUsuario = new SelectList(db.TipoUsuarios, "Id_TipoUsuario", "Nombre", usuario.Id_TipoUsuario);
            return View(usuario);
        }

        // GET PARA EDITAR PERFIL

        [HttpGet]
        public ActionResult EditProfile(int? id)
        {
            if (Session["UserId"] == null)
            {
                return RedirectToAction("Login", "Auth"); // DEVOLVER A LOGIN SI NO HAY USUARIO LOGUEADO
            }

            // LLENAR EL ID CON EL USUARIO LOGUEADO
            int userId = id ?? Convert.ToInt32(Session["UserId"]);

            var usuario = db.Usuarios.FirstOrDefault(u => u.id_Usuario == userId);
            if (usuario == null)
            {
                return RedirectToAction("Login", "Auth");
            }

            // ADMIN PUEDE EDITAR CUALQUIER USUARIO
            if (IsAdminUser())
            {
                ViewBag.IsAdmin = true;
            }

            return View(usuario);
        }

        // POST PARA EDITAR PERFIL
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditProfile(Usuario updatedUser, HttpPostedFileBase ImgURL)
        {

            // REDIRECCIONAR A LOGIN EN CASO NO ESTAR LOGUEADO
            if (Session["UserId"] == null)
            {
                return RedirectToAction("Login", "Auth");
            }

            try
            {
                // OBTENER EL USUARIO A EDITAR
                int userId = Convert.ToInt32(Session["UserId"]);
                var currentUser = db.Usuarios.FirstOrDefault(u => u.id_Usuario == userId);

                if (currentUser == null)
                {
                    return RedirectToAction("Login", "Auth");
                }

                // EDICION DESDE LA VISTA DE ADMIN
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

                // VALIDAR MODELO
                if (string.IsNullOrWhiteSpace(updatedUser.Username))
                {
                    ModelState.AddModelError("Username", "El nombre de usuario no puede estar vacío");
                    return View(usuarioAEditar);
                }

                // VERIFICAR SI EL NOMBRE DE USUARIO YA EXISTE
                if (db.Usuarios.Any(u => u.Username == updatedUser.Username && u.id_Usuario != usuarioAEditar.id_Usuario))
                {
                    ModelState.AddModelError("Username", "Este nombre de usuario ya está en uso");
                    return View(usuarioAEditar);
                }

                // ACTUALIZAR USERNAME
                usuarioAEditar.Username = updatedUser.Username;

                // MANEJO PARA LA IMAGEN DE PERFIL
                if (ImgURL != null && ImgURL.ContentLength > 0)
                {
                    var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
                    var extension = Path.GetExtension(ImgURL.FileName).ToLower();

                    if (!allowedExtensions.Contains(extension))
                    {
                        ModelState.AddModelError("ImgURL", "Solo se permiten imágenes (.jpg, .jpeg, .png, .gif)");
                        return View(usuarioAEditar);
                    }

                    // CREAR DIRECTORIO EN CASO DE NO EXISTIR
                    var imagesPath = Server.MapPath("~/images");
                    if (!Directory.Exists(imagesPath))
                    {
                        Directory.CreateDirectory(imagesPath);
                    }

                    var fileName = $"profile_{usuarioAEditar.id_Usuario}_{DateTime.Now.Ticks}{extension}";
                    var imageUrl = "/images/" + fileName;
                    var path = Path.Combine(imagesPath, fileName);

                    // ELIMINAR IMAGEN ANTERIOR
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

                // GUARDAR CAMBIOS
                db.Entry(usuarioAEditar).State = EntityState.Modified;
                int changes = db.SaveChanges();

                if (changes > 0)
                {
                    // ACTUALIZAR SESION DEL USUARIO LOGUEADO
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
        // MOSTRAR DASHBOARD PARA USUARIOS
        public ActionResult Dashboard()
        {
            if (Session["UserType"] == null || Session["Username"] == null)
            {
                return RedirectToAction("Login", "Auth"); // REDIRIGIR SI NO ESTA LOGUEADO
            }

            // OBTENER ID DE USUARIO PARA LA SESION
            int userId = Convert.ToInt32(Session["UserId"]);
            var usuario = db.Usuarios.FirstOrDefault(u => u.id_Usuario == userId);

            if (usuario == null)
            {
                return RedirectToAction("Login", "Auth"); // SI NO ENCONTRAR ID REDIRIGIR A LOGIN
            }

            ViewBag.UserName = usuario.Username;

            // CASO SWITCH PARA MOSTRAR DASBOARD DEPENDIENDO DEL TIPO USUARIO
            int userType = Convert.ToInt32(Session["UserType"]);

            switch (userType)
            {
                case 1: // ADMINISTRADOR
                    //CARGAR FOLLOWS Y FOLLOWERS
                    ViewBag.FollowerCount = db.Seguidors.Count(s => s.Id_UsuarioSiguiendo == userId);
                    ViewBag.FollowingCount = db.Seguidors.Count(s => s.Id_UsuarioSeguidor == userId);
                    return View("AdminDashboard", usuario); // RETORNA ADMIN DASHBOARD

                case 2: // ARTISTA
                    //CARGAR EL ARTISTA DEL USUARIO LOGUEADO
                    var artista = db.Artistas.FirstOrDefault(a => a.Id_Usuario == userId);
                    if (artista != null)
                    {  //CARGAR FOLLOWS Y FOLLOWERS
                        ViewBag.FollowerCount = db.Seguidors.Count(s => s.Id_UsuarioSiguiendo == userId);
                        ViewBag.FollowingCount = db.Seguidors.Count(s => s.Id_UsuarioSeguidor == userId);
                        ViewBag.ArtistaInfo = artista;
                    }
                    //LISTA DE USUARIOS QUE SIGUEN AL USUARIO
                    ViewBag.FollowingList = db.Seguidors
                                              .Where(s => s.Id_UsuarioSeguidor == userId)
                                              .Select(s => s.Usuario1) // INFORMACION DE USUARIO SEGUIDO
                                              .ToList();
                    return View("ArtistDashboard", usuario); // REDIRIGIR AL DASHBOARD DE ARTISTA

                case 3: // USUARIO COMUN
                    ViewBag.FollowerCount = db.Seguidors.Count(s => s.Id_UsuarioSiguiendo == userId);
                    ViewBag.FollowingCount = db.Seguidors.Count(s => s.Id_UsuarioSeguidor == userId);
                    // LISTA DE FOLLOWERS DEL USUARIO
                    ViewBag.FollowingList = db.Seguidors
                                              .Where(s => s.Id_UsuarioSeguidor == userId)
                                              .Select(s => s.Usuario1) //INFORMACION DE USUARIO SEGUIDO
                                              .ToList();
                    return View("UserDashboard", usuario); // REDIRIGIR AL DASBOARD DE USUARIO COMUN

                case 4: // DISQUERA
                    //CARGAR LA DISQUERA DEL USUARIO LOGUEADO
                    var disquera = db.Disqueras.FirstOrDefault(d => d.Id_Usuario == userId);
                    if (disquera != null)
                    {
                        ViewBag.FollowerCount = db.Seguidors.Count(s => s.Id_UsuarioSiguiendo == userId);
                        ViewBag.FollowingCount = db.Seguidors.Count(s => s.Id_UsuarioSeguidor == userId);
                        ViewBag.DisqueraInfo = disquera;
                    }
                    // LISTA DE FOLLOWERS DEL USUARIO LOGUEADO
                    ViewBag.FollowingList = db.Seguidors
                                              .Where(s => s.Id_UsuarioSeguidor == userId)
                                              .Select(s => s.Usuario1) // INFORMACION DEL USUARIO SEGUIDO
                                              .ToList();
                    return View("DisqueraDashboard", usuario); // RETORNA AL DASHBOARD DE DISQUERA

                case 5: // PRODUCTOR
                    var productor = db.Productors.FirstOrDefault(p => p.Id_Usuario == userId);
                    if (productor != null)
                    {
                        ViewBag.FollowerCount = db.Seguidors.Count(s => s.Id_UsuarioSiguiendo == userId);
                        ViewBag.FollowingCount = db.Seguidors.Count(s => s.Id_UsuarioSeguidor == userId);
                        ViewBag.ProductorInfo = productor;
                    }
                    // LISTA DE FOLLOWERS DE USUARIO LOGUEADO
                    ViewBag.FollowingList = db.Seguidors
                                              .Where(s => s.Id_UsuarioSeguidor == userId)
                                              .Select(s => s.Usuario1) // INFORMACION DE USUARIO SEGUIDO
                                              .ToList();
                    return View("ProductorDashboard", usuario); // RETORNAR A DASHBOARD

                case 6: // DIRECTOR
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
        // Acción GET para mostrar la confirmación de eliminación
        [HttpPost, ActionName("DeleteUser")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(long id)
        {
            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    // Obtener usuario con todas sus relaciones
                    var usuario = db.Usuarios
                        .Include(u => u.Artistas)
                        .Include(u => u.Disqueras)
                        .Include(u => u.Productors)
                        .Include(u => u.Directors)
                        .Include(u => u.Seguidors) // Los que él sigue
                        .Include(u => u.Seguidors1) // Los que lo siguen
                        .Include(u => u.Votoes) // Votos del usuario
                        .FirstOrDefault(u => u.id_Usuario == id);

                    if (usuario == null)
                    {
                        TempData["ErrorMessage"] = "Usuario no encontrado";
                        return RedirectToAction("ManageUsers");
                    }

                    // 1. Eliminar relaciones de seguidores
                    var seguidores = db.Seguidors
                        .Where(s => s.Id_UsuarioSeguidor == id || s.Id_UsuarioSiguiendo == id)
                        .ToList();
                    db.Seguidors.RemoveRange(seguidores);

                    // 2. Eliminar votos del usuario
                    var votos = db.Votoes.Where(v => v.Id_Usuario == id).ToList();
                    db.Votoes.RemoveRange(votos);

                    // 3. Manejar cada tipo de usuario específico
                    switch (usuario.Id_TipoUsuario)
                    {
                        case 2: // Artista
                            var artista = db.Artistas
                                .Include(a => a.Albums)
                                .Include(a => a.Cancions)
                                .FirstOrDefault(a => a.Id_Usuario == id);

                            if (artista != null)
                            {
                                // Eliminar canciones y sus archivos
                                foreach (var cancion in artista.Cancions.ToList())
                                {

                                    // Eliminar imagen de canción
                                    if (!string.IsNullOrEmpty(cancion.ImgURL) && !cancion.ImgURL.Contains("default"))
                                    {
                                        var imgPath = Server.MapPath(cancion.ImgURL);
                                        if (System.IO.File.Exists(imgPath))
                                        {
                                            System.IO.File.Delete(imgPath);
                                        }
                                    }
                                    db.Cancions.Remove(cancion);
                                }

                                // Eliminar álbumes y sus imágenes
                                foreach (var album in artista.Albums.ToList())
                                {
                                    if (!string.IsNullOrEmpty(album.ImgURL) && !album.ImgURL.Contains("default"))
                                    {
                                        var imgPath = Server.MapPath(album.ImgURL);
                                        if (System.IO.File.Exists(imgPath))
                                        {
                                            System.IO.File.Delete(imgPath);
                                        }
                                    }
                                    db.Albums.Remove(album);
                                }

                                db.Artistas.Remove(artista);
                            }
                            break;

                        case 4: // Disquera
                            var disquera = db.Disqueras.FirstOrDefault(d => d.Id_Usuario == id);
                            if (disquera != null)
                            {
                                // Actualizar artistas que pertenecían a esta disquera
                                var artistas = db.Artistas.Where(a => a.Id_Disquera == disquera.Id_Disquera).ToList();
                                foreach (var art in artistas)
                                {
                                    art.Id_Disquera = null;
                                }
                                db.Disqueras.Remove(disquera);
                            }
                            break;

                        case 5: // Productor
                            var productor = db.Productors
                                .Include(p => p.Series)
                                .Include(p => p.Peliculas)
                                .FirstOrDefault(p => p.Id_Usuario == id);

                            if (productor != null)
                            {
                                // Eliminar series relacionadas
                                foreach (var serie in productor.Series.ToList())
                                {
                                    EliminarSerieCompleta(serie.Id_Serie);
                                }

                                // Eliminar películas relacionadas
                                foreach (var pelicula in productor.Peliculas.ToList())
                                {
                                    EliminarPelicula(pelicula.Id_pelicula);
                                }

                                db.Productors.Remove(productor);
                            }
                            break;

                        case 6: // Director
                            var director = db.Directors
                                .Include(d => d.Series)
                                .Include(d => d.Peliculas)
                                .FirstOrDefault(d => d.Id_Usuario == id);

                            if (director != null)
                            {
                                // Eliminar series relacionadas
                                foreach (var serie in director.Series.ToList())
                                {
                                    EliminarSerieCompleta(serie.Id_Serie);
                                }

                                // Eliminar películas relacionadas
                                foreach (var pelicula in director.Peliculas.ToList())
                                {
                                    EliminarPelicula(pelicula.Id_pelicula);
                                }

                                db.Directors.Remove(director);
                            }
                            break;
                    }

                    // 4. Eliminar imagen de perfil
                    if (!string.IsNullOrEmpty(usuario.ImgURL) && !usuario.ImgURL.Contains("default-profile"))
                    {
                        var imagePath = Server.MapPath(usuario.ImgURL);
                        if (System.IO.File.Exists(imagePath))
                        {
                            System.IO.File.Delete(imagePath);
                        }
                    }

                    // 5. Eliminar el usuario
                    db.Usuarios.Remove(usuario);
                    db.SaveChanges();
                    transaction.Commit();

                    // Si el usuario se está eliminando a sí mismo, cerrar sesión
                    if (Session["UserId"] != null && Convert.ToInt64(Session["UserId"]) == id)
                    {
                        Session.Clear();
                        TempData["SuccessMessage"] = "Tu cuenta y todo tu contenido han sido eliminados permanentemente.";
                        return RedirectToAction("Index", "Home");
                    }

                    TempData["SuccessMessage"] = "Usuario y todo su contenido relacionado eliminados correctamente.";
                    return RedirectToAction("ManageUsers");
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    System.Diagnostics.Debug.WriteLine($"Error al eliminar usuario: {ex}");
                    TempData["ErrorMessage"] = $"Error al eliminar el usuario: {ex.Message}";
                    return RedirectToAction("DeleteUser", new { id = id });
                }
            }
        }

        // Métodos auxiliares para eliminar contenido relacionado
        private void EliminarSerieCompleta(long serieId)
        {
            var serie = db.Series
                .Include(s => s.Temporadas)
                .Include(s => s.Contenidoes)
                .FirstOrDefault(s => s.Id_Serie == serieId);

            if (serie != null)
            {
                // Eliminar capítulos
                foreach (var temporada in serie.Temporadas.ToList())
                {
                    var capitulos = db.Capituloes.Where(c => c.Id_Temporada == temporada.Id_Temporada).ToList();
                    db.Capituloes.RemoveRange(capitulos);
                    db.Temporadas.Remove(temporada);
                }

                // Eliminar imagen de serie
                if (!string.IsNullOrEmpty(serie.ImgURL) && !serie.ImgURL.Contains("default"))
                {
                    var imgPath = Server.MapPath(serie.ImgURL);
                    if (System.IO.File.Exists(imgPath))
                    {
                        System.IO.File.Delete(imgPath);
                    }
                }

                // Eliminar contenido relacionado
                db.Contenidoes.RemoveRange(serie.Contenidoes);
                db.Series.Remove(serie);
            }
        }

        private void EliminarPelicula(long peliculaId)
        {
            var pelicula = db.Peliculas
                .Include(p => p.Contenidoes)
                .FirstOrDefault(p => p.Id_pelicula == peliculaId);

            if (pelicula != null)
            {
                // Eliminar imagen de película
                if (!string.IsNullOrEmpty(pelicula.ImgURL) && !pelicula.ImgURL.Contains("default"))
                {
                    var imgPath = Server.MapPath(pelicula.ImgURL);
                    if (System.IO.File.Exists(imgPath))
                    {
                        System.IO.File.Delete(imgPath);
                    }
                }

                // Eliminar contenido relacionado
                db.Contenidoes.RemoveRange(pelicula.Contenidoes);
                db.Peliculas.Remove(pelicula);
            }
        }
    }
}
