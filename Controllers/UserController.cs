using PARCIALDBEveriflix.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;

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

        // Acción para gestionar usuarios (ver lista de usuarios)
        public ActionResult ManageUsers()
        {
            if (!IsAdminUser())
            {
                TempData["ErrorMessage"] = "Acceso denegado: Se requieren privilegios de administrador";
                return RedirectToAction("Login", "Auth");
            }

            var usuarios = db.Usuarios.Include(u => u.TipoUsuario).ToList();
            return View(usuarios);
        }

        // Acción GET para editar usuario
        public ActionResult EditUser(int id)
        {
            if (!IsAdminUser())
            {
                return RedirectToAction("Login", "Auth"); // Redirige si no es admin
            }

            var usuario = db.Usuarios.Find(id);
            if (usuario == null)
            {
                return HttpNotFound();
            }

            // Obtener lista de tipos de usuario
            ViewBag.TiposUsuario = new SelectList(db.TipoUsuarios, "id_TipoUsuario", "Nombre", usuario.Id_TipoUsuario);
            return View(usuario);
        }

        // Acción POST para editar usuario
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditUser(int id, Usuario usuario)
        {
            if (!IsAdminUser()) // Verificar si es administrador
            {
                TempData["ErrorMessage"] = "Acceso denegado: Se requieren privilegios de administrador";
                return RedirectToAction("Login", "Auth");
            }

            try
            {
                // Verificar si el id de la URL coincide con el id del modelo
                if (id != usuario.id_Usuario)
                {
                    TempData["ErrorMessage"] = "Inconsistencia en el ID del usuario";
                    return RedirectToAction("ManageUsers");
                }

                if (ModelState.IsValid)
                {
                    // Buscar el usuario en la base de datos
                    var usuarioExistente = db.Usuarios.Find(id);
                    if (usuarioExistente == null)
                    {
                        return HttpNotFound();
                    }

                    // Actualizar los campos del usuario
                    usuarioExistente.Username = usuario.Username;
                    usuarioExistente.Correo = usuario.Correo;
                    usuarioExistente.Id_TipoUsuario = usuario.Id_TipoUsuario;

                    // Marcar como modificado
                    db.Entry(usuarioExistente).State = System.Data.Entity.EntityState.Modified;

                    // Guardar los cambios en la base de datos
                    db.SaveChanges();

                    // Verificar si los cambios se guardaron correctamente
                    TempData["SuccessMessage"] = "Usuario actualizado correctamente";

                    return RedirectToAction("ManageUsers");
                }
                // Si el modelo no es válido, devolver la vista con el error
                ViewBag.TiposUsuario = new SelectList(db.TipoUsuarios, "id_TipoUsuario", "Nombre", usuario.Id_TipoUsuario);
                return View(usuario);
            }
            catch (Exception ex)
            {
                // Capturar cualquier error
                System.Diagnostics.Debug.WriteLine($"Error al actualizar usuario: {ex.Message}");
                TempData["ErrorMessage"] = "Error al guardar los cambios";
                ViewBag.TiposUsuario = new SelectList(db.TipoUsuarios, "id_TipoUsuario", "Nombre", usuario.Id_TipoUsuario);
                return View(usuario);
            }
        }

        // Acción para eliminar usuario
        public ActionResult DeleteUser(int? id)
        {
            if (id == null)
            {
                return RedirectToAction("ManageUsers");
            }

            // Verificar permisos de administrador
            if (!IsAdminUser())
            {
                TempData["ErrorMessage"] = "Acceso denegado: Se requieren privilegios de administrador";
                return RedirectToAction("Login", "Auth");
            }

            Usuario usuario = db.Usuarios.Find(id);
            if (usuario == null)
            {
                return HttpNotFound();
            }

            return View(usuario);
        }

        // POST: User/DeleteUser/5
        [HttpPost, ActionName("DeleteUser")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            // Verificar permisos de administrador
            if (!IsAdminUser())
            {
                return RedirectToAction("Login", "Auth");
            }

            Usuario usuario = db.Usuarios.Find(id);
            db.Usuarios.Remove(usuario);
            db.SaveChanges();
            return RedirectToAction("ManageUsers");
        }
    


public ActionResult UsersIndex()
        {
            // Verificar que el usuario sea administrador (tipo 1)
            if (Session["UserType"] == null || (int)Session["UserType"] != 1)
            {
                return RedirectToAction("Login", "Auth");
            }

            var usuarios = db.Usuarios.Include(u => u.TipoUsuario).ToList();
            return View(usuarios);
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
                    return View("AdminDashboard", usuario);

                case 2: // Artista
                    var artista = db.Artistas.FirstOrDefault(a => a.Id_Usuario == userId);
                    if (artista != null)
                    {
                        ViewBag.ArtistaInfo = artista;
                        return View("ArtistDashboard", usuario);
                    }
                    return View("ArtistDashboard", usuario);

                case 3: // Comun (Usuario normal)
                    return View("UserDashboard", usuario);

                case 4: // Disquera
                    var disquera = db.Disqueras.FirstOrDefault(d => d.Id_Usuario == userId);
                    if (disquera != null)
                    {
                        ViewBag.DisqueraInfo = disquera;
                        return View("DisqueraDashboard", usuario);
                    }
                    return View("DisqueraDashboard", usuario);

                case 5: // Productor
                    var productor = db.Productors.FirstOrDefault(p => p.Id_Usuario == userId);
                    if (productor != null)
                    {
                        ViewBag.ProductorInfo = productor;
                        return View("ProducerDashboard", usuario);
                    }
                    return View("ProducerDashboard", usuario);

                case 6: // Director
                    var director = db.Directors.FirstOrDefault(d => d.Id_Usuario == userId);
                    if (director != null)
                    {
                        ViewBag.DirectorInfo = director;
                        return View("DirectorDashboard", usuario);
                    }
                    return View("DirectorDashboard", usuario);

                default:
                    ViewBag.ErrorMessage = "Tipo de usuario desconocido.";
                    return RedirectToAction("Login", "Auth");
            }
        }

        // Acción para editar perfil
        public ActionResult EditProfile()
        {
            if (Session["UserId"] == null)
            {
                return RedirectToAction("Login", "Auth"); // Si no está logueado, redirigir al login
            }

            int userId = Convert.ToInt32(Session["UserId"]);
            var usuario = db.Usuarios.FirstOrDefault(u => u.id_Usuario == userId);

            if (usuario == null)
            {
                return RedirectToAction("Login", "Auth");
            }

            int userType = Convert.ToInt32(Session["UserType"]);

            // Agregar información adicional según el tipo de usuario
            switch (userType)
            {
                case 2: // Artista
                    ViewBag.ArtistaInfo = db.Artistas.FirstOrDefault(a => a.Id_Usuario == userId);
                    break;
                case 4: // Disquera
                    ViewBag.DisqueraInfo = db.Disqueras.FirstOrDefault(d => d.Id_Usuario == userId);
                    break;
                case 5: // Productor
                    ViewBag.ProductorInfo = db.Productors.FirstOrDefault(p => p.Id_Usuario == userId);
                    break;
                case 6: // Director
                    ViewBag.DirectorInfo = db.Directors.FirstOrDefault(d => d.Id_Usuario == userId);
                    break;
            }

            return View(usuario);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditProfile(Usuario updatedUser, HttpPostedFileBase ImgURL)
        {
            if (!IsAdminUser())
            {
                return RedirectToAction("Dashboard"); // Redirige a su dashboard correspondiente
            }

            int userId = Convert.ToInt32(Session["UserId"]);
            var usuario = db.Usuarios.FirstOrDefault(u => u.id_Usuario == userId);

            if (usuario == null)
            {
                return RedirectToAction("Login", "Auth");
            }

            usuario.Correo = updatedUser.Correo;

            // Si se sube una nueva imagen
            if (ImgURL != null && ImgURL.ContentLength > 0)
            {
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
                var extension = Path.GetExtension(ImgURL.FileName).ToLower();

                if (allowedExtensions.Contains(extension))
                {
                    var fileName = Path.GetFileName(ImgURL.FileName);
                    var imageUrl = "/images/" + fileName;
                    var path = Path.Combine(Server.MapPath("~/images"), fileName);
                    ImgURL.SaveAs(path);

                    usuario.ImgURL = imageUrl;
                }
                else
                {
                    ViewBag.ErrorMessage = "Solo se permiten imágenes (.jpg, .jpeg, .png, .gif)";
                    return View(usuario);
                }
            }

            db.Entry(usuario).State = System.Data.Entity.EntityState.Modified;
            db.SaveChanges();

            // Actualizar información específica según el tipo de usuario
            int userType = Convert.ToInt32(Session["UserType"]);
            switch (userType)
            {
                case 2: // Artista
                    var artista = db.Artistas.FirstOrDefault(a => a.Id_Usuario == userId);
                    if (artista != null)
                    {
                        artista.Nombre = updatedUser.Username;
                        db.SaveChanges();
                    }
                    break;

                case 4: // Disquera
                    var disquera = db.Disqueras.FirstOrDefault(d => d.Id_Usuario == userId);
                    if (disquera != null)
                    {
                        disquera.Nombre = updatedUser.Username;
                        db.SaveChanges();
                    }
                    break;

                case 5: // Productor
                    var productor = db.Productors.FirstOrDefault(p => p.Id_Usuario == userId);
                    if (productor != null)
                    {
                        productor.Nombre = updatedUser.Username;
                        db.SaveChanges();
                    }
                    break;

                case 6: // Director
                    var director = db.Directors.FirstOrDefault(d => d.Id_Usuario == userId);
                    if (director != null)
                    {
                        director.Nombre = updatedUser.Username;
                        db.SaveChanges();
                    }
                    break;
            }

            return RedirectToAction("Dashboard");
        }
    }
}