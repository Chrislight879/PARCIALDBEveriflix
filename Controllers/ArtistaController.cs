using PARCIALDBEveriflix.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Data.Entity;

public class ArtistaController : Controller
{
    private EveryflixDBEntities db = new EveryflixDBEntities();
    [AuthorizeArtista]
    public ActionResult ManageMusic()
    {
        long userId = GetCurrentUserId();
        if (userId == 0)
        {
            TempData["ErrorMessage"] = "Debes iniciar sesión";
            return RedirectToAction("Login", "Auth");
        }

        var artista = db.Artistas
            .Include(a => a.Disquera)
            .Include(a => a.Usuario)
            .FirstOrDefault(a => a.Id_Usuario == userId);

        if (artista == null)
        {
            TempData["ErrorMessage"] = "No tienes un perfil de artista";
            return RedirectToAction("Create");
        }

        // Obtener todas las disqueras disponibles (excepto la actual si tiene una)
        var disquerasDisponibles = db.Disqueras.ToList();
        if (artista.Id_Disquera != null)
        {
            disquerasDisponibles = disquerasDisponibles
                .Where(d => d.Id_Disquera != artista.Id_Disquera)
                .ToList();
        }

        ViewBag.Artista = artista;
        ViewBag.Albumes = db.Albums
            .Where(a => a.Id_Artista == artista.Id_Artista)
            .Include(a => a.Cancions)
            .OrderByDescending(a => a.Id_Album)
            .ToList();

        ViewBag.Canciones = db.Cancions
            .Where(c => c.Id_Artista == artista.Id_Artista)
            .Include(c => c.Album)
            .Include(c => c.GeneroCancion)
            .OrderByDescending(c => c.Id_Cancion)
            .ToList();

        ViewBag.DisquerasDisponibles = disquerasDisponibles;

        return View();
    }
    [AuthorizeArtista]
    public ActionResult Create()
    {
        if (GetCurrentArtista() != null)
        {
            TempData["WarningMessage"] = "Ya tienes un perfil de artista";
            return RedirectToAction("ManageMusic");
        }

        ViewBag.Disqueras = new SelectList(db.Disqueras, "Id_Disquera", "Nombre");
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [AuthorizeArtista]
    public ActionResult Create(Artista artista)
    {
        if (ModelState.IsValid)
        {
            artista.Id_Usuario = GetCurrentUserId();
            db.Artistas.Add(artista);

            if (db.SaveChanges() > 0)
            {
                TempData["SuccessMessage"] = "Perfil de artista creado exitosamente";
                return RedirectToAction("ManageMusic");
            }
            else
            {
                TempData["ErrorMessage"] = "No se pudo crear el perfil";
            }
        }

        ViewBag.Disqueras = new SelectList(db.Disqueras, "Id_Disquera", "Nombre", artista.Id_Disquera);
        return View(artista);
    }

    [AuthorizeArtista]
    public ActionResult Edit()
    {
        var artista = GetCurrentArtista();
        if (artista == null)
        {
            TempData["ErrorMessage"] = "Primero debes crear tu perfil de artista";
            return RedirectToAction("Create");
        }

        ViewBag.Disqueras = new SelectList(db.Disqueras, "Id_Disquera", "Nombre", artista.Id_Disquera);
        return View(artista);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [AuthorizeArtista]
    public ActionResult Edit(Artista artista)
    {
        var currentArtista = GetCurrentArtista();
        if (currentArtista == null)
        {
            TempData["ErrorMessage"] = "Artista no encontrado";
            return RedirectToAction("ManageMusic");
        }

        if (ModelState.IsValid)
        {
            currentArtista.Nombre = artista.Nombre;
            currentArtista.Id_Disquera = artista.Id_Disquera;

            if (db.SaveChanges() > 0)
            {
                TempData["SuccessMessage"] = "Perfil actualizado exitosamente";
            }
            else
            {
                TempData["WarningMessage"] = "No se realizaron cambios en el perfil";
            }

            return RedirectToAction("ManageMusic");
        }

        ViewBag.Disqueras = new SelectList(db.Disqueras, "Id_Disquera", "Nombre", artista.Id_Disquera);
        return View(artista);
    }

    [AuthorizeArtista]
    public ActionResult CreateAlbum()
    {
        return View();
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    [AuthorizeArtista]
    public ActionResult CreateAlbum(Album album, HttpPostedFileBase imagen)
    {
        var artista = GetCurrentArtista();
        if (artista == null)
        {
            TempData["ErrorMessage"] = "Debes tener un perfil de artista";
            return RedirectToAction("Create");
        }

        if (ModelState.IsValid)
        {
            try
            {
                album.ImgURL = HandleAlbumImage(imagen);
                album.Id_Artista = artista.Id_Artista;
                db.Albums.Add(album);

                if (db.SaveChanges() > 0)
                {
                    TempData["SuccessMessage"] = "Álbum creado exitosamente";
                    return RedirectToAction("ManageMusic");
                }
                else
                {
                    TempData["ErrorMessage"] = "No se pudo crear el álbum";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al crear álbum: {ex.Message}");
                TempData["ErrorMessage"] = "Error al crear el álbum";
            }
        }

        return View(album);
    }

    [AuthorizeArtista]
    public ActionResult EditAlbum(long id)
    {
        var album = db.Albums.Find(id);
        var artista = GetCurrentArtista();

        if (album == null || album.Id_Artista != artista?.Id_Artista)
        {
            TempData["ErrorMessage"] = "Álbum no encontrado o no tienes permiso";
            return RedirectToAction("ManageMusic");
        }

        return View(album);
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    [AuthorizeArtista]
    public ActionResult EditAlbum(Album album, HttpPostedFileBase imagen)
    {
        var artista = GetCurrentArtista();
        if (artista == null) return RedirectToAction("ManageMusic");

        var albumExistente = db.Albums.Find(album.Id_Album);
        if (albumExistente == null || albumExistente.Id_Artista != artista.Id_Artista)
        {
            TempData["ErrorMessage"] = "Álbum no encontrado o no tienes permiso";
            return RedirectToAction("ManageMusic");
        }

        if (ModelState.IsValid)
        {
            try
            {
                if (imagen != null && imagen.ContentLength > 0)
                {
                    // Eliminar imagen anterior si existe
                    if (!string.IsNullOrEmpty(albumExistente.ImgURL) &&
                        albumExistente.ImgURL != "/Content/Images/AlbumCovers/default.jpg")
                    {
                        var oldPath = Server.MapPath(albumExistente.ImgURL);
                        if (System.IO.File.Exists(oldPath))
                        {
                            System.IO.File.Delete(oldPath);
                        }
                    }

                    albumExistente.ImgURL = HandleAlbumImage(imagen);
                }

                albumExistente.Nombre = album.Nombre;
                db.Entry(albumExistente).State = EntityState.Modified;

                if (db.SaveChanges() > 0)
                {
                    TempData["SuccessMessage"] = "Álbum actualizado correctamente";
                }
                else
                {
                    TempData["WarningMessage"] = "No se realizaron cambios en el álbum";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al editar álbum: {ex.Message}");
                TempData["ErrorMessage"] = "Error al actualizar el álbum";
                return View(album);
            }

            return RedirectToAction("ManageMusic");
        }

        return View(album);
    }

    [AuthorizeArtista]
    public ActionResult CreateSong()
    {
        var artista = GetCurrentArtista();
        if (artista == null)
        {
            TempData["ErrorMessage"] = "Debes tener un perfil de artista";
            return RedirectToAction("Create");
        }

        ViewBag.Id_Album = GetAlbumsForArtist(artista.Id_Artista);
        ViewBag.Id_GeneroCancion = new SelectList(db.GeneroCancions, "Id_GeneroCancion", "Nombre");

        return View(new Cancion
        {
            Id_TipoContenido = 3,
            Duracion = TimeSpan.FromMinutes(3)
        });
    }


    [HttpPost]
    [ValidateAntiForgeryToken]
    [AuthorizeArtista]
    public ActionResult CreateSong(Cancion cancion, HttpPostedFileBase imagen)
    {
        var artista = GetCurrentArtista();
        if (artista == null)
        {
            TempData["ErrorMessage"] = "Debes tener un perfil de artista";
            return RedirectToAction("Create");
        }

        if (ModelState.IsValid)
        {
            try
            {
                cancion.ImgURL = HandleSongImage(imagen);
                cancion.Id_Artista = artista.Id_Artista;
                cancion.Id_TipoContenido = 3;

                if (cancion.Id_Album == 0)
                    cancion.Id_Album = null;

                db.Cancions.Add(cancion);

                if (db.SaveChanges() > 0)
                {
                    TempData["SuccessMessage"] = "Canción creada exitosamente!";
                    return RedirectToAction("ManageMusic");
                }
                else
                {
                    TempData["ErrorMessage"] = "No se pudo crear la canción";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al crear canción: {ex.Message}");
                ModelState.AddModelError("", "Error al guardar la canción: " + ex.Message);
            }
        }

        ViewBag.Id_Album = GetAlbumsForArtist(artista.Id_Artista, cancion.Id_Album);
        ViewBag.Id_GeneroCancion = new SelectList(db.GeneroCancions, "Id_GeneroCancion", "Nombre", cancion.Id_GeneroCancion);
        return View(cancion);
    }

    [AuthorizeArtista]
    public ActionResult EditSong(long id)
    {
        var cancion = db.Cancions.Find(id);
        var artista = GetCurrentArtista();

        if (cancion == null || cancion.Id_Artista != artista?.Id_Artista)
        {
            TempData["ErrorMessage"] = "Canción no encontrada o no tienes permiso";
            return RedirectToAction("ManageMusic");
        }

        ViewBag.Albums = new SelectList(
            db.Albums.Where(a => a.Id_Artista == artista.Id_Artista),
            "Id_Album",
            "Nombre",
            cancion.Id_Album);

        ViewBag.Generos = new SelectList(
            db.GeneroCancions,
            "Id_GeneroCancion",
            "Nombre",
            cancion.Id_GeneroCancion);

        return View(cancion);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [AuthorizeArtista]
    public ActionResult EditSong(Cancion cancion, HttpPostedFileBase imagen)
    {
        var artista = GetCurrentArtista();
        if (artista == null) return RedirectToAction("ManageMusic");

        if (!ModelState.IsValid)
        {
            ViewBag.Albums = GetAlbumsForArtist(artista.Id_Artista, cancion.Id_Album);
            ViewBag.Generos = new SelectList(db.GeneroCancions, "Id_GeneroCancion", "Nombre", cancion.Id_GeneroCancion);
            return View(cancion);
        }

        var cancionExistente = db.Cancions.Find(cancion.Id_Cancion);
        if (cancionExistente == null || cancionExistente.Id_Artista != artista.Id_Artista)
        {
            TempData["ErrorMessage"] = "Canción no encontrada o no tienes permiso";
            return RedirectToAction("ManageMusic");
        }

        try
        {
            if (imagen != null && imagen.ContentLength > 0)
            {
                // Eliminar imagen anterior si existe
                if (!string.IsNullOrEmpty(cancionExistente.ImgURL) &&
                    cancionExistente.ImgURL != "/Content/Images/SongCovers/default.jpg")
                {
                    var oldPath = Server.MapPath(cancionExistente.ImgURL);
                    if (System.IO.File.Exists(oldPath))
                    {
                        System.IO.File.Delete(oldPath);
                    }
                }

                cancionExistente.ImgURL = HandleSongImage(imagen);
            }

            cancionExistente.Nombre = cancion.Nombre;
            cancionExistente.Duracion = cancion.Duracion;
            cancionExistente.Id_Album = cancion.Id_Album;
            cancionExistente.Id_GeneroCancion = cancion.Id_GeneroCancion;

            db.Entry(cancionExistente).State = EntityState.Modified;

            if (db.SaveChanges() > 0)
            {
                TempData["SuccessMessage"] = "Canción actualizada correctamente";
            }
            else
            {
                TempData["WarningMessage"] = "No se realizaron cambios en la canción";
            }

            return RedirectToAction("ManageMusic");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error al editar canción: {ex.Message}");
            TempData["ErrorMessage"] = "Error al guardar los cambios de la canción";
            ViewBag.Albums = GetAlbumsForArtist(artista.Id_Artista, cancion.Id_Album);
            ViewBag.Generos = new SelectList(db.GeneroCancions, "Id_GeneroCancion", "Nombre", cancion.Id_GeneroCancion);
            return View(cancion);
        }
    }
    [AuthorizeArtista]
    public ActionResult DeleteSong(long id)
    {
        var cancion = db.Cancions.Find(id);
        var artista = GetCurrentArtista();

        if (cancion == null || cancion.Id_Artista != artista?.Id_Artista)
        {
            TempData["ErrorMessage"] = "Canción no encontrada o no tienes permiso";
            return RedirectToAction("ManageMusic");
        }

        return View(cancion);
    }

    [HttpPost, ActionName("DeleteSong")]
    [ValidateAntiForgeryToken]
    [AuthorizeArtista]
    public ActionResult DeleteSongConfirmed(long id)
    {
        var artista = GetCurrentArtista();
        if (artista == null) return RedirectToAction("ManageMusic");

        using (var transaction = db.Database.BeginTransaction())
        {
            try
            {
                var cancion = db.Cancions.Find(id);
                if (cancion == null || cancion.Id_Artista != artista.Id_Artista)
                {
                    TempData["ErrorMessage"] = "Canción no encontrada o no tienes permiso";
                    return RedirectToAction("ManageMusic");
                }

                db.Cancions.Remove(cancion);

                if (db.SaveChanges() > 0)
                {
                    transaction.Commit();
                    TempData["SuccessMessage"] = "Canción eliminada correctamente";
                }
                else
                {
                    transaction.Rollback();
                    TempData["ErrorMessage"] = "No se pudo eliminar la canción";
                }
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                TempData["ErrorMessage"] = "Error al eliminar: " + ex.Message;
            }
        }

        return RedirectToAction("ManageMusic");
    }

    [AuthorizeArtista]
    public ActionResult JoinDisquera(long id)
    {
        var artista = GetCurrentArtista();
        if (artista == null)
        {
            TempData["ErrorMessage"] = "Debes tener un perfil de artista";
            return RedirectToAction("Create");
        }

        try
        {
            var disquera = db.Disqueras.Find(id);
            if (disquera == null)
            {
                TempData["ErrorMessage"] = "Disquera no encontrada";
                return RedirectToAction("ManageMusic");
            }

            // Verificar si ya está asociado a esta disquera
            if (artista.Id_Disquera == id)
            {
                TempData["WarningMessage"] = "Ya estás asociado a esta disquera";
                return RedirectToAction("ManageMusic");
            }

            artista.Id_Disquera = id;
            db.Entry(artista).State = EntityState.Modified;

            if (db.SaveChanges() > 0)
            {
                TempData["SuccessMessage"] = $"Te has unido exitosamente a la disquera {disquera.Nombre}";
            }
            else
            {
                TempData["ErrorMessage"] = "No se pudo completar la operación";
            }
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error al unirse a la disquera: {ex.Message}";
            // Log del error
            System.Diagnostics.Debug.WriteLine($"Error en JoinDisquera: {ex}");
        }

        return RedirectToAction("ManageMusic");
    }

    [AuthorizeArtista]
    public ActionResult LeaveDisquera()
    {
        var artista = GetCurrentArtista();
        if (artista == null)
        {
            TempData["ErrorMessage"] = "Debes tener un perfil de artista";
            return RedirectToAction("Create");
        }

        try
        {
            // Verificar si ya no está asociado a ninguna disquera
            if (artista.Id_Disquera == null)
            {
                TempData["WarningMessage"] = "No estás asociado a ninguna disquera";
                return RedirectToAction("ManageMusic");
            }

            var nombreDisquera = artista.Disquera?.Nombre ?? "la disquera";
            artista.Id_Disquera = null;
            db.Entry(artista).State = EntityState.Modified;

            if (db.SaveChanges() > 0)
            {
                TempData["SuccessMessage"] = $"Has abandonado exitosamente {nombreDisquera}";
            }
            else
            {
                TempData["ErrorMessage"] = "No se pudo completar la operación";
            }
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error al abandonar la disquera: {ex.Message}";
            // Log del error
            System.Diagnostics.Debug.WriteLine($"Error en LeaveDisquera: {ex}");
        }

        return RedirectToAction("ManageMusic");
    }

    private bool IsArtista()
    {
        if (Session["UserId"] == null || Session["UserType"] == null)
            return false;

        int userType = Convert.ToInt32(Session["UserType"]);
        return userType == 2;
    }

    private long GetCurrentUserId()
    {
        if (Session["UserId"] == null)
            return 0;

        return Convert.ToInt64(Session["UserId"]);
    }

    private Artista GetCurrentArtista()
    {
        long userId = GetCurrentUserId();
        return db.Artistas.FirstOrDefault(a => a.Id_Usuario == userId);
    }

    private SelectList GetAlbumsForArtist(long artistaId, object selectedValue = null)
    {
        var albumes = db.Albums
            .Where(a => a.Id_Artista == artistaId)
            .Select(a => new { a.Id_Album, a.Nombre })
            .ToList();

        var albumList = new SelectList(albumes, "Id_Album", "Nombre", selectedValue);
        var items = albumList.ToList();
        items.Insert(0, new SelectListItem { Text = "(Sin álbum)", Value = "" });

        return new SelectList(items, "Value", "Text", selectedValue);
    }

    // Método para manejar imágenes de canciones
    private string HandleSongImage(HttpPostedFileBase imagen)
    {
        if (imagen == null || imagen.ContentLength == 0)
            return "/Content/Images/SongCovers/default.jpg";

        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
        var extension = Path.GetExtension(imagen.FileName).ToLower();

        if (!allowedExtensions.Contains(extension))
        {
            TempData["ErrorMessage"] = "Solo se permiten imágenes (.jpg, .jpeg, .png, .gif)";
            return "/Content/Images/SongCovers/default.jpg";
        }

        try
        {
            // Crear directorio si no existe
            var songCoversPath = Server.MapPath("~/Content/Images/SongCovers");
            if (!Directory.Exists(songCoversPath))
            {
                Directory.CreateDirectory(songCoversPath);
            }

            var fileName = $"song_{DateTime.Now.Ticks}{extension}";
            var imageUrl = "/Content/Images/SongCovers/" + fileName;
            var path = Path.Combine(songCoversPath, fileName);

            imagen.SaveAs(path);
            return imageUrl;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error al guardar imagen de canción: {ex.Message}");
            TempData["ErrorMessage"] = "Error al guardar la imagen de la canción";
            return "/Content/Images/SongCovers/default.jpg";
        }
    }
    // Método para manejar imágenes de álbumes
    private string HandleAlbumImage(HttpPostedFileBase imagen)
    {
        if (imagen == null || imagen.ContentLength == 0)
            return "/Content/Images/AlbumCovers/default.jpg";

        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
        var extension = Path.GetExtension(imagen.FileName).ToLower();

        if (!allowedExtensions.Contains(extension))
        {
            TempData["ErrorMessage"] = "Solo se permiten imágenes (.jpg, .jpeg, .png, .gif)";
            return "/Content/Images/AlbumCovers/default.jpg";
        }

        try
        {
            // Crear directorio si no existe
            var albumCoversPath = Server.MapPath("~/Content/Images/AlbumCovers");
            if (!Directory.Exists(albumCoversPath))
            {
                Directory.CreateDirectory(albumCoversPath);
            }

            var fileName = $"album_{DateTime.Now.Ticks}{extension}";
            var imageUrl = "/Content/Images/AlbumCovers/" + fileName;
            var path = Path.Combine(albumCoversPath, fileName);

            imagen.SaveAs(path);
            return imageUrl;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error al guardar imagen de álbum: {ex.Message}");
            TempData["ErrorMessage"] = "Error al guardar la imagen del álbum";
            return "/Content/Images/AlbumCovers/default.jpg";
        }
    }
}

public class AuthorizeArtistaAttribute : AuthorizeAttribute
{
    protected override bool AuthorizeCore(HttpContextBase httpContext)
    {
        if (httpContext.Session["UserType"] == null)
            return false;

        int userType = Convert.ToInt32(httpContext.Session["UserType"]);
        return userType == 2;
    }

    protected override void HandleUnauthorizedRequest(AuthorizationContext filterContext)
    {
        filterContext.Result = new RedirectToRouteResult(
            new System.Web.Routing.RouteValueDictionary {
                { "controller", "Home" },
                { "action", "Index" },
                { "errorMessage", "Acceso restringido a artistas" }
            });
    }

   
    
}
