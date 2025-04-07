using System;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using PARCIALDBEveriflix.Models;
using System.Collections.Generic;
using System.Data.Entity.Validation;
using System.IO;
using System.Web;
using System.Net;

public class TemporadaController : Controller
{
    private EveryflixDBEntities db = new EveryflixDBEntities();

    private bool EsUsuarioPermitido()
    {
        if (Session["UserId"] == null || Session["UserType"] == null)
        {
            System.Diagnostics.Debug.WriteLine("Error: Faltan datos de sesión (UserId o UserType)");
            return false;
        }

        try
        {
            long userId = (long)Session["UserId"];
            long userType = (long)Session["UserType"];

            switch (userType)
            {
                case 5: // Productor
                    return db.Productors.Any(p => p.Id_Usuario == userId);

                case 6: // Director
                    return db.Directors.Any(d => d.Id_Usuario == userId);

                default:
                    System.Diagnostics.Debug.WriteLine($"Tipo de usuario no permitido: {userType}");
                    return false;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error en EsUsuarioPermitido: {ex.Message}");
            return false;
        }
    }

    private bool VerificarPermisosSerie(Serie serie)
    {
        if (Session["UserId"] == null || Session["UserType"] == null)
            return false;

        long userId = (long)Session["UserId"];
        long userType = (long)Session["UserType"];

        if (userType == 5) // Productor
        {
            var productor = db.Productors.FirstOrDefault(p => p.Id_Usuario == userId);
            return productor != null && serie.Id_Productor == productor.Id_Productor;
        }
        else if (userType == 6) // Director
        {
            var director = db.Directors.FirstOrDefault(d => d.Id_Usuario == userId);
            return director != null && serie.Id_Director == director.Id_Director;
        }

        return false;
    }

    // GET: ManageSeasons - Vista principal de gestión de temporadas
    public ActionResult ManageSeasons(long idSerie)
    {
        if (!EsUsuarioPermitido())
        {
            TempData["ErrorMessage"] = "Acceso denegado";
            return RedirectToAction("Index", "Home");
        }

        var serie = db.Series
            .Include(s => s.Temporadas)
            .Include(s => s.Categoria)
            .Include(s => s.Clasificacion)
            .Include(s => s.Productor)
            .Include(s => s.Director)
            .FirstOrDefault(s => s.Id_Serie == idSerie);

        if (serie == null)
        {
            TempData["ErrorMessage"] = "Serie no encontrada";
            return HttpNotFound();
        }

        if (!VerificarPermisosSerie(serie))
        {
            TempData["ErrorMessage"] = "No tiene permisos para gestionar esta serie";
            return RedirectToAction("Index", "Home");
        }

        // Ordenar temporadas por nombre
        var temporadas = serie.Temporadas.OrderBy(t => t.Nombre).ToList();

        ViewBag.Serie = serie;
        ViewBag.SerieNombre = serie.Nombre;
        ViewBag.IdSerie = serie.Id_Serie;

        return View(temporadas);
    }

    public ActionResult CreateTemporada(long idSerie)
    {
        if (!EsUsuarioPermitido())
        {
            TempData["ErrorMessage"] = "Acceso denegado";
            return RedirectToAction("ManageSeasons", new { idSerie = idSerie });
        }

        var serie = db.Series
            .Include(s => s.Temporadas)
            .FirstOrDefault(s => s.Id_Serie == idSerie);

        if (serie == null)
        {
            TempData["ErrorMessage"] = "Serie no encontrada";
            return RedirectToAction("Index", "Serie");
        }

        if (!VerificarPermisosSerie(serie))
        {
            TempData["ErrorMessage"] = "No tiene permisos para gestionar esta serie";
            return RedirectToAction("ManageSeasons", new { idSerie = idSerie });
        }

        ViewBag.Title = "Crear Temporada para " + serie.Nombre;
        ViewBag.SerieNombre = serie.Nombre;
        ViewBag.IdSerie = idSerie;
        ViewBag.Serie = serie;

        var temporada = new Temporada
        {
            Id_Serie = idSerie,
            Nombre = "Temporada " + (serie.Temporadas.Count + 1)
        };

        return View(temporada);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public ActionResult CreateTemporada(Temporada temporada)
    {
        if (!EsUsuarioPermitido())
        {
            TempData["ErrorMessage"] = "Acceso denegado";
            return RedirectToAction("ManageSeasons", new { idSerie = temporada.Id_Serie });
        }

        var serie = db.Series.Find(temporada.Id_Serie);
        if (serie == null)
        {
            TempData["ErrorMessage"] = "Serie no encontrada";
            return RedirectToAction("ManageSeasons", new { idSerie = temporada.Id_Serie });
        }

        if (!VerificarPermisosSerie(serie))
        {
            TempData["ErrorMessage"] = "No tiene permisos para gestionar esta serie";
            return RedirectToAction("ManageSeasons", new { idSerie = temporada.Id_Serie });
        }

        if (ModelState.IsValid)
        {
            try
            {
                db.Temporadas.Add(temporada);
                db.SaveChanges();
                TempData["SuccessMessage"] = "Temporada creada exitosamente";
                return RedirectToAction("ManageSeasons", new { idSerie = temporada.Id_Serie });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Error al guardar: " + ex.Message);
            }
        }

        // Si hay errores, recargar los datos necesarios para la vista
        ViewBag.Title = "Crear Temporada para " + serie.Nombre;
        ViewBag.SerieNombre = serie.Nombre;
        ViewBag.IdSerie = temporada.Id_Serie;
        ViewBag.Serie = serie;

        return View(temporada);
    }

    public ActionResult EditTemporada(long id)
    {
        if (!EsUsuarioPermitido())
        {
            TempData["ErrorMessage"] = "Acceso denegado";
            return RedirectToAction("ManageSeasons", new { idSerie = db.Temporadas.Find(id)?.Id_Serie });
        }

        var temporada = db.Temporadas
            .Include(t => t.Serie)
            .Include(t => t.Capituloes)
            .FirstOrDefault(t => t.Id_Temporada == id);

        if (temporada == null)
        {
            TempData["ErrorMessage"] = "Temporada no encontrada";
            return RedirectToAction("ManageSeasons", new { idSerie = db.Temporadas.Find(id)?.Id_Serie });
        }

        if (!VerificarPermisosSerie(temporada.Serie))
        {
            TempData["ErrorMessage"] = "No tiene permisos para gestionar esta serie";
            return RedirectToAction("ManageSeasons", new { idSerie = temporada.Id_Serie });
        }

        ViewBag.Title = "Editar " + temporada.Nombre;
        ViewBag.SerieNombre = temporada.Serie.Nombre;
        ViewBag.IdSerie = temporada.Id_Serie;
        ViewBag.Serie = temporada.Serie;

        return View(temporada);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public ActionResult EditTemporada(Temporada temporada)
    {
        if (!EsUsuarioPermitido())
        {
            TempData["ErrorMessage"] = "Acceso denegado";
            return RedirectToAction("ManageSeasons", new { idSerie = temporada.Id_Serie });
        }

        var tempOriginal = db.Temporadas.Find(temporada.Id_Temporada);
        if (tempOriginal == null)
        {
            TempData["ErrorMessage"] = "Temporada no encontrada";
            return RedirectToAction("ManageSeasons", new { idSerie = temporada.Id_Serie });
        }

        if (!VerificarPermisosSerie(tempOriginal.Serie))
        {
            TempData["ErrorMessage"] = "No tiene permisos para gestionar esta serie";
            return RedirectToAction("ManageSeasons", new { idSerie = tempOriginal.Id_Serie });
        }

        if (ModelState.IsValid)
        {
            try
            {
                // Actualizar solo los campos necesarios
                tempOriginal.Nombre = temporada.Nombre;

                db.Entry(tempOriginal).State = EntityState.Modified;
                db.SaveChanges();

                TempData["SuccessMessage"] = "Temporada actualizada exitosamente";
                return RedirectToAction("ManageSeasons", new { idSerie = tempOriginal.Id_Serie });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Error al guardar: " + ex.Message);
            }
        }

        // Si hay errores, recargar los datos necesarios
        ViewBag.Title = "Editar " + temporada.Nombre;
        ViewBag.SerieNombre = tempOriginal.Serie.Nombre;
        ViewBag.IdSerie = tempOriginal.Id_Serie;
        ViewBag.Serie = tempOriginal.Serie;

        return View(temporada);
    }
    // GET: Eliminar Temporada
    // GET: Eliminar Temporada (Vista de confirmación)
    public ActionResult DeleteTemporada(long? id)
    {
        if (!EsUsuarioPermitido() || id == null)
        {
            TempData["ErrorMessage"] = "Acceso denegado";
            return RedirectToAction("Index", "Home");
        }

        var temporada = db.Temporadas
            .Include(t => t.Serie)
            .Include(t => t.Capituloes)
            .FirstOrDefault(t => t.Id_Temporada == id);

        if (temporada == null)
        {
            TempData["ErrorMessage"] = "Temporada no encontrada";
            return HttpNotFound();
        }

        if (!VerificarPermisosSerie(temporada.Serie))
        {
            TempData["ErrorMessage"] = "No tiene permisos para gestionar esta serie";
            return RedirectToAction("ManageSeasons", new { idSerie = temporada.Id_Serie });
        }

        // Pasar los datos necesarios a la vista
        ViewBag.SerieNombre = temporada.Serie.Nombre;
        ViewBag.IdSerie = temporada.Id_Serie;
        ViewBag.Serie = temporada.Serie;

        return View(temporada);
    }

   [HttpPost]
[ValidateAntiForgeryToken]
public ActionResult DeleteConfirmedTemporada(long id, long idSerie)
{
    try
    {
        var temporada = db.Temporadas
            .Include(t => t.Capituloes)
            .FirstOrDefault(t => t.Id_Temporada == id);

        if (temporada != null)
        {
            // Eliminar capítulos primero
            if (temporada.Capituloes != null && temporada.Capituloes.Any())
            {
                db.Capituloes.RemoveRange(temporada.Capituloes);
            }

            // Eliminar temporada
            db.Temporadas.Remove(temporada);
            db.SaveChanges();
        }

        if (Request.IsAjaxRequest())
        {
            return Json(new { success = true });
        }
        
        TempData["SuccessMessage"] = "Temporada eliminada correctamente";
        return RedirectToAction("ManageSeasons", new { idSerie = idSerie });
    }
    catch (Exception ex)
    {
        if (Request.IsAjaxRequest())
        {
            return Json(new { success = false });
        }
        
        TempData["ErrorMessage"] = "Error al eliminar la temporada: " + ex.Message;
        return RedirectToAction("DeleteTemporada", new { id = id });
    }
}
    public ActionResult ManageCapitulos(long idTemporada)
    {
        if (!EsUsuarioPermitido())
        {
            TempData["ErrorMessage"] = "Acceso denegado";
            return RedirectToAction("Index", "Home");
        }

        // Obtener la temporada con su serie y capítulos relacionados
        var temporada = db.Temporadas
            .Include(t => t.Capituloes)
            .Include(t => t.Serie)  // Asegurar que se incluye la relación Serie
            .FirstOrDefault(t => t.Id_Temporada == idTemporada);

        if (temporada == null)
        {
            TempData["ErrorMessage"] = "Temporada no encontrada";
            return HttpNotFound();
        }

        if (!VerificarPermisosSerie(temporada.Serie))
        {
            TempData["ErrorMessage"] = "No tiene permisos para gestionar esta serie";
            return RedirectToAction("ManageSeasons", new { idSerie = temporada.Id_Serie });
        }

        // Preparar los datos para la vista
        ViewBag.Temporada = temporada;
        ViewBag.TemporadaNombre = temporada.Nombre;
        ViewBag.SerieNombre = temporada.Serie.Nombre;

        // CAMBIO CLAVE: Usar ImgURL de la Serie directamente
        ViewBag.SerieImagen = temporada.Serie.ImgURL;

        // Si la imagen está vacía, usar default.jpg
        if (string.IsNullOrEmpty(ViewBag.SerieImagen))
        {
            ViewBag.SerieImagen = "/Content/Images/SeriesCovers/default.jpg";
        }

        ViewBag.IdTemporada = temporada.Id_Temporada;
        ViewBag.IdSerie = temporada.Id_Serie;
        ViewBag.TotalCapitulos = temporada.Capituloes.Count;

        // Ordenar capítulos por número (asumiendo que existe propiedad Numero)
        var capitulos = temporada.Capituloes.OrderBy(c => c.Nombre).ToList();
        return View(capitulos);
    }
    // GET: Crear Capítulo
    public ActionResult CreateCapitulo(long idTemporada)
    {
        if (!EsUsuarioPermitido())
        {
            TempData["ErrorMessage"] = "Acceso denegado";
            return RedirectToAction("ManageSeasons", new { idSerie = db.Temporadas.Find(idTemporada)?.Id_Serie });
        }

        var temporada = db.Temporadas
            .Include(t => t.Serie)
            .FirstOrDefault(t => t.Id_Temporada == idTemporada);

        if (temporada == null)
        {
            TempData["ErrorMessage"] = "Temporada no encontrada";
            return RedirectToAction("ManageSeasons", new { idSerie = db.Temporadas.Find(idTemporada)?.Id_Serie });
        }

        if (!VerificarPermisosSerie(temporada.Serie))
        {
            TempData["ErrorMessage"] = "No tiene permisos para gestionar esta serie";
            return RedirectToAction("ManageSeasons", new { idSerie = temporada.Id_Serie });
        }

        var capitulo = new Capitulo
        {
            Id_Temporada = idTemporada,
            Duracion = new TimeSpan(0, 45, 0) // Duración por defecto: 45 minutos
        };

        ViewBag.TemporadaNombre = temporada.Nombre;
        ViewBag.SerieNombre = temporada.Serie.Nombre;
        ViewBag.IdTemporada = idTemporada;
        ViewBag.IdSerie = temporada.Id_Serie;

        return View(capitulo);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public ActionResult CreateCapitulo(Capitulo capitulo)
    {
        if (!EsUsuarioPermitido())
        {
            TempData["ErrorMessage"] = "Acceso denegado";
            return RedirectToAction("ManageSeasons", new { idSerie = db.Temporadas.Find(capitulo.Id_Temporada)?.Id_Serie });
        }

        var temporada = db.Temporadas
            .Include(t => t.Serie)
            .FirstOrDefault(t => t.Id_Temporada == capitulo.Id_Temporada);

        if (temporada == null)
        {
            TempData["ErrorMessage"] = "Temporada no encontrada";
            return RedirectToAction("ManageSeasons", new { idSerie = db.Temporadas.Find(capitulo.Id_Temporada)?.Id_Serie });
        }

        if (!VerificarPermisosSerie(temporada.Serie))
        {
            TempData["ErrorMessage"] = "No tiene permisos para gestionar esta serie";
            return RedirectToAction("ManageSeasons", new { idSerie = temporada.Id_Serie });
        }

        if (ModelState.IsValid)
        {
            try
            {
                db.Capituloes.Add(capitulo);
                db.SaveChanges();
                TempData["SuccessMessage"] = "Capítulo creado exitosamente";
                return RedirectToAction("ManageCapitulos", new { idTemporada = capitulo.Id_Temporada });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Error al guardar: " + ex.Message);
            }
        }

        ViewBag.TemporadaNombre = temporada.Nombre;
        ViewBag.SerieNombre = temporada.Serie.Nombre;
        ViewBag.IdTemporada = capitulo.Id_Temporada;
        ViewBag.IdSerie = temporada.Id_Serie;

        return View(capitulo);
    }

    // GET: Editar Capítulo
    public ActionResult EditCapitulo(long? id)
    {
        if (!EsUsuarioPermitido() || id == null)
        {
            TempData["ErrorMessage"] = "Acceso denegado";
            return RedirectToAction("Index", "Home");
        }

        var capitulo = db.Capituloes
            .Include(c => c.Temporada)
            .Include(c => c.Temporada.Serie)
            .FirstOrDefault(c => c.Id_Capitulo == id);

        if (capitulo == null)
        {
            TempData["ErrorMessage"] = "Capítulo no encontrado";
            return HttpNotFound();
        }

        if (!VerificarPermisosSerie(capitulo.Temporada.Serie))
        {
            TempData["ErrorMessage"] = "No tiene permisos para gestionar esta serie";
            return RedirectToAction("ManageSeasons", new { idSerie = capitulo.Temporada.Id_Serie });
        }

        ViewBag.TemporadaNombre = capitulo.Temporada.Nombre;
        ViewBag.SerieNombre = capitulo.Temporada.Serie.Nombre;
        ViewBag.IdTemporada = capitulo.Id_Temporada;
        ViewBag.IdSerie = capitulo.Temporada.Id_Serie;

        return View(capitulo);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public ActionResult EditCapitulo(Capitulo capitulo)
    {
        if (!EsUsuarioPermitido())
        {
            TempData["ErrorMessage"] = "Acceso denegado";
            return RedirectToAction("ManageSeasons", new { idSerie = db.Capituloes.Find(capitulo.Id_Capitulo)?.Temporada?.Id_Serie });
        }

        // Obtener la entidad original desde la base de datos
        var capOriginal = db.Capituloes.Find(capitulo.Id_Capitulo);

        if (capOriginal == null)
        {
            TempData["ErrorMessage"] = "Capítulo no encontrado";
            return RedirectToAction("ManageSeasons", new { idSerie = db.Capituloes.Find(capitulo.Id_Capitulo)?.Temporada?.Id_Serie });
        }

        // Verificar permisos
        var temporada = db.Temporadas.Include(t => t.Serie).FirstOrDefault(t => t.Id_Temporada == capOriginal.Id_Temporada);
        if (!VerificarPermisosSerie(temporada?.Serie))
        {
            TempData["ErrorMessage"] = "No tiene permisos para gestionar esta serie";
            return RedirectToAction("ManageSeasons", new { idSerie = temporada?.Id_Serie });
        }

        if (ModelState.IsValid)
        {
            try
            {
                // Actualizar solo las propiedades necesarias
                capOriginal.Nombre = capitulo.Nombre;
                capOriginal.Duracion = capitulo.Duracion;
                // Actualizar otras propiedades según sea necesario

                db.Entry(capOriginal).State = EntityState.Modified;
                db.SaveChanges();

                TempData["SuccessMessage"] = "Capítulo actualizado exitosamente";
                return RedirectToAction("ManageCapitulos", new { idTemporada = capOriginal.Id_Temporada });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Error al guardar: " + ex.Message);
                System.Diagnostics.Debug.WriteLine($"Error al editar capítulo: {ex}");
            }
        }

        // Si hay errores, recargar los datos necesarios para la vista
        ViewBag.TemporadaNombre = temporada?.Nombre;
        ViewBag.SerieNombre = temporada?.Serie?.Nombre;
        ViewBag.IdTemporada = capOriginal.Id_Temporada;
        ViewBag.IdSerie = temporada?.Id_Serie;

        return View(capitulo);
    }

    // GET: Temporada/DeleteCapitulo/5
    public ActionResult DeleteCapitulo(long? id)
    {
        if (id == null)
        {
            TempData["ErrorMessage"] = "ID no válido.";
            return RedirectToAction("Index"); // O a donde quieras redirigir
        }

        // Recupera el capítulo basado en el ID
        var capitulo = db.Capituloes
            .Include(c => c.Temporada)
            .FirstOrDefault(c => c.Id_Capitulo == id);

        if (capitulo == null)
        {
            TempData["ErrorMessage"] = "Capítulo no encontrado.";
            return HttpNotFound();
        }

        return View(capitulo); // Regresa la vista para confirmar la eliminación
    }

    [HttpPost, ActionName("DeleteCapitulo")]
    [ValidateAntiForgeryToken]
    public ActionResult DeleteConfirmedCapitulo(long id)
    {
        try
        {
            // Encuentra el capítulo en la base de datos
            var capitulo = db.Capituloes
                .Include(c => c.Temporada)
                .FirstOrDefault(c => c.Id_Capitulo == id);

            if (capitulo == null)
            {
                TempData["ErrorMessage"] = "Capítulo no encontrado.";
                return HttpNotFound();
            }

            // Guardamos el id de la temporada antes de eliminar
            var idTemporada = capitulo.Id_Temporada;

            // Eliminar el capítulo
            db.Capituloes.Remove(capitulo);
            db.SaveChanges();

            TempData["SuccessMessage"] = "Capítulo eliminado correctamente.";
            return RedirectToAction("ManageCapitulos", new { idTemporada = idTemporada });
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = "Error al eliminar el capítulo: " + ex.Message;
            return RedirectToAction("DeleteCapitulo", new { id = id });
        }
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            db?.Dispose();
        }
        base.Dispose(disposing);
    }
}