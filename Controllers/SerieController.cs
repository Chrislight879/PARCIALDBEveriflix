using System;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using PARCIALDBEveriflix.Models;
using System.Collections.Generic;
using System.Data.Entity.Validation;
using System.IO;
using System.Web;

public class SerieController : Controller
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

    // GET: Series para Productores
    public ActionResult ManageSeriesProductor()
    {
        if (!EsUsuarioPermitido() || (long)Session["UserType"] != 5)
        {
            TempData["ErrorMessage"] = "Acceso denegado. Solo los productores pueden gestionar series.";
            return RedirectToAction("Index", "Home");
        }

        long userId = (long)Session["UserId"];
        var productor = db.Productors.FirstOrDefault(p => p.Id_Usuario == userId);

        if (productor == null)
        {
            TempData["ErrorMessage"] = "No tiene un perfil de productor válido";
            return RedirectToAction("CompleteProfile", "Account");
        }

        var series = db.Series
            .Include(s => s.Director)
            .Include(s => s.Categoria)
            .Include(s => s.Clasificacion)
            .Where(s => s.Id_Productor == productor.Id_Productor)
            .OrderBy(s => s.Nombre)
            .ToList();

        ViewBag.NombreProductor = productor.Nombre;
        return View(series);
    }

    // GET: Series para Directores
    public ActionResult ManageSeriesDirector()
    {
        try
        {
            if (Session["UserId"] == null || Session["UserType"] == null)
            {
                TempData["ErrorMessage"] = "Debe iniciar sesión";
                return RedirectToAction("Login", "Account");
            }

            if ((long)Session["UserType"] != 6)
            {
                TempData["ErrorMessage"] = "Acceso no autorizado";
                return RedirectToAction("Index", "Home");
            }

            long userId = (long)Session["UserId"];
            var director = db.Directors
                            .Include(d => d.Series)
                            .FirstOrDefault(d => d.Id_Usuario == userId);

            if (director == null)
            {
                TempData["ErrorMessage"] = "Perfil de director no encontrado";
                return RedirectToAction("CompleteProfile", "Account");
            }

            var series = db.Series
                .Include(s => s.Productor)
                .Include(s => s.Categoria)
                .Include(s => s.Clasificacion)
                .Where(s => s.Id_Director == director.Id_Director)
                .OrderBy(s => s.Nombre)
                .ToList();

            ViewBag.NombreDirector = director.Nombre;
            return View(series);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error en ManageSeriesDirector: {ex}");
            TempData["ErrorMessage"] = "Error al cargar series";
            return RedirectToAction("Index", "Home");
        }
    }

    // GET: Crear Serie (Productor)
    public ActionResult CreateProductor()
    {
        if (!EsUsuarioPermitido() || (long)Session["UserType"] != 5)
        {
            TempData["ErrorMessage"] = "Acceso denegado. Solo los productores pueden crear series.";
            return RedirectToAction("Index", "Home");
        }

        // Cargar listas con AsNoTracking() para mejor rendimiento
        ViewBag.Id_Categoria = new SelectList(
            db.Categorias.AsNoTracking().OrderBy(c => c.Nombre).ToList(),
            "Id_Categoria", "Nombre");

        ViewBag.Id_Clasificacion = new SelectList(
            db.Clasificacions.AsNoTracking().OrderBy(c => c.Nombre).ToList(),
            "Id_Clasificacion", "Nombre");

        // Directores para productores
        var directores = db.Directors
            .AsNoTracking()
            .OrderBy(d => d.Nombre)
            .Select(d => new SelectListItem
            {
                Value = d.Id_Director.ToString(),
                Text = d.Nombre
            }).ToList();

        directores.Insert(0, new SelectListItem { Value = "", Text = "Seleccione director..." });
        ViewBag.Id_Director = directores;

        long userId = (long)Session["UserId"];
        var productor = db.Productors.FirstOrDefault(p => p.Id_Usuario == userId);

        var tipoContenido = db.TipoContenidoes.FirstOrDefault(t => t.Nombre == "Serie");
        if (tipoContenido == null)
        {
            tipoContenido = new TipoContenido { Nombre = "Serie" };
            db.TipoContenidoes.Add(tipoContenido);
            db.SaveChanges();
        }

        var nuevaSerie = new Serie
        {
            ImgURL = "/Content/Images/SeriesCovers/default.jpg",
            Id_Productor = productor?.Id_Productor ?? 0,
            Id_TipoContenido = tipoContenido.Id_TipoContenido
        };

        return View(nuevaSerie);
    }

    // GET: Crear Serie (Director)
    // GET: Crear Serie (Director)
    public ActionResult CreateDirector()
    {
        try
        {
            if (Session["UserId"] == null || Session["UserType"] == null)
            {
                TempData["ErrorMessage"] = "Debe iniciar sesión para acceder a esta función";
                return RedirectToAction("Login", "Account");
            }

            if ((long)Session["UserType"] != 6)
            {
                TempData["ErrorMessage"] = "No tiene permisos para acceder a esta sección";
                return RedirectToAction("Index", "Home");
            }

            long userId = (long)Session["UserId"];
            var director = db.Directors.FirstOrDefault(d => d.Id_Usuario == userId);

            if (director == null)
            {
                TempData["ErrorMessage"] = "No tiene un perfil de director configurado";
                return RedirectToAction("CompleteProfile", "Account");
            }

            // Cargar categorías
            ViewBag.Id_Categoria = new SelectList(
                db.Categorias.AsNoTracking().OrderBy(c => c.Nombre).ToList(),
                "Id_Categoria", "Nombre");

            // Cargar clasificaciones
            ViewBag.Id_Clasificacion = new SelectList(
                db.Clasificacions.AsNoTracking().OrderBy(c => c.Nombre).ToList(),
                "Id_Clasificacion", "Nombre");

            // Cargar productores (opcional)
            var productores = db.Productors
                .AsNoTracking()
                .OrderBy(p => p.Nombre)
                .Select(p => new SelectListItem
                {
                    Value = p.Id_Productor.ToString(),
                    Text = p.Nombre
                }).ToList();

            productores.Insert(0, new SelectListItem { Value = "", Text = "Seleccione productor (opcional)..." });
            ViewBag.Id_Productor = productores;

            // Pasar información del director
            ViewBag.DirectorId = director.Id_Director;
            ViewBag.DirectorNombre = director.Nombre;

            var nuevaSerie = new Serie
            {
                ImgURL = "/Content/Images/SeriesCovers/default.jpg",
                Id_Director = director.Id_Director,
                Id_TipoContenido = db.TipoContenidoes.First(t => t.Nombre == "Serie").Id_TipoContenido
            };

            return View(nuevaSerie);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error en CreateDirector: {ex}");
            TempData["ErrorMessage"] = "Ocurrió un error inesperado al cargar el formulario";
            return RedirectToAction("ManageSeriesDirector");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public ActionResult CreateProductor(Serie serie, HttpPostedFileBase imagen)
    {
        if (!EsUsuarioPermitido() || (long)Session["UserType"] != 5)
        {
            TempData["ErrorMessage"] = "Acceso denegado. Solo los productores pueden crear series.";
            return RedirectToAction("Index", "Home");
        }

        long userId = (long)Session["UserId"];
        var productor = db.Productors.FirstOrDefault(p => p.Id_Usuario == userId);

        if (productor == null)
        {
            TempData["ErrorMessage"] = "No tiene un perfil de productor válido";
            return RedirectToAction("CompleteProfile", "Account");
        }

        serie.Id_Productor = productor.Id_Productor;
        serie.Id_TipoContenido = db.TipoContenidoes.First(t => t.Nombre == "Serie").Id_TipoContenido;

        if (ModelState.IsValid)
        {
            if (imagen != null && imagen.ContentLength > 0)
            {
                var resultadoImagen = GuardarImagenSerie(imagen);
                if (resultadoImagen == null)
                {
                    ModelState.AddModelError("ImgURL", "Error al procesar la imagen. Formatos válidos: JPG, PNG, GIF");
                }
                else
                {
                    serie.ImgURL = resultadoImagen;
                }
            }

            if (ModelState.IsValid)
            {
                try
                {
                    db.Series.Add(serie);
                    db.SaveChanges();

                    // Crear registro en tabla Contenido
                    var contenido = new Contenido
                    {
                        Id_Serie = serie.Id_Serie
                    };
                    db.Contenidoes.Add(contenido);
                    db.SaveChanges();

                    TempData["SuccessMessage"] = "Serie creada exitosamente";
                    return RedirectToAction("ManageSeriesProductor");
                }
                catch (DbEntityValidationException ex)
                {
                    foreach (var eve in ex.EntityValidationErrors)
                    {
                        foreach (var ve in eve.ValidationErrors)
                        {
                            ModelState.AddModelError(ve.PropertyName, ve.ErrorMessage);
                        }
                    }
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "Error al guardar: " + ex.Message);
                }
            }
        }

        CargarListasParaSerie(serie);
        return View(serie);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public ActionResult CreateDirector(Serie serie, HttpPostedFileBase imagen, int DirectorId)
    {
        try
        {
            // Asignar el ID del director
            serie.Id_Director = DirectorId;

            // Asignar tipo de contenido
            serie.Id_TipoContenido = db.TipoContenidoes.First(t => t.Nombre == "Serie").Id_TipoContenido;

            if (ModelState.IsValid)
            {
                // Manejo de la imagen
                if (imagen != null && imagen.ContentLength > 0)
                {
                    // Validaciones de imagen...
                    serie.ImgURL = GuardarImagenSerie(imagen);
                }
                else
                {
                    serie.ImgURL = "/Content/Images/SeriesCovers/default.jpg";
                }

                db.Series.Add(serie);
                db.SaveChanges();

                TempData["SuccessMessage"] = "Serie creada exitosamente!";
                return RedirectToAction("ManageSeriesDirector");
            }

            // Si hay errores, recargar las listas
            ViewBag.Id_Categoria = new SelectList(
                db.Categorias.AsNoTracking().OrderBy(c => c.Nombre).ToList(),
                "Id_Categoria", "Nombre", serie.Id_Categoria);

            ViewBag.Id_Clasificacion = new SelectList(
                db.Clasificacions.AsNoTracking().OrderBy(c => c.Nombre).ToList(),
                "Id_Clasificacion", "Nombre", serie.Id_Clasificacion);

            var productores = db.Productors
                .AsNoTracking()
                .OrderBy(p => p.Nombre)
                .Select(p => new SelectListItem
                {
                    Value = p.Id_Productor.ToString(),
                    Text = p.Nombre,
                    Selected = serie.Id_Productor == p.Id_Productor
                }).ToList();

            productores.Insert(0, new SelectListItem { Value = "", Text = "Seleccione productor (opcional)..." });
            ViewBag.Id_Productor = productores;

            // Volver a pasar la información del director
            var director = db.Directors.Find(DirectorId);
            ViewBag.DirectorId = DirectorId;
            ViewBag.DirectorNombre = director?.Nombre;

            return View(serie);
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = "Error al crear la serie: " + ex.Message;
            return RedirectToAction("CreateDirector");
        }
    }

    // GET: Editar Serie (Productor)
    public ActionResult EditProductor(long? id)
    {
        if (!EsUsuarioPermitido() || (long)Session["UserType"] != 5 || id == null)
        {
            TempData["ErrorMessage"] = "Acceso denegado. Solo los productores pueden editar series.";
            return RedirectToAction("ManageSeriesProductor");
        }

        long userId = (long)Session["UserId"];
        var productor = db.Productors.FirstOrDefault(p => p.Id_Usuario == userId);

        if (productor == null)
        {
            TempData["ErrorMessage"] = "No tiene un perfil de productor válido";
            return RedirectToAction("CompleteProfile", "Account");
        }

        var serie = db.Series
            .Include(s => s.Categoria)
            .Include(s => s.Clasificacion)
            .Include(s => s.Director)
            .FirstOrDefault(s => s.Id_Serie == id && s.Id_Productor == productor.Id_Productor);

        if (serie == null)
        {
            TempData["ErrorMessage"] = "Serie no encontrada o no tiene permisos para editarla";
            return HttpNotFound();
        }

        // Cargar listas con los datos actuales
        ViewBag.Id_Categoria = new SelectList(
            db.Categorias.AsNoTracking().OrderBy(c => c.Nombre).ToList(),
            "Id_Categoria", "Nombre", serie.Id_Categoria);

        ViewBag.Id_Clasificacion = new SelectList(
            db.Clasificacions.AsNoTracking().OrderBy(c => c.Nombre).ToList(),
            "Id_Clasificacion", "Nombre", serie.Id_Clasificacion);

        // Directores para productores
        var directores = db.Directors
            .AsNoTracking()
            .OrderBy(d => d.Nombre)
            .Select(d => new SelectListItem
            {
                Value = d.Id_Director.ToString(),
                Text = d.Nombre,
                Selected = serie.Id_Director == d.Id_Director
            }).ToList();

        directores.Insert(0, new SelectListItem { Value = "", Text = "Seleccione director..." });
        ViewBag.Id_Director = directores;

        return View(serie);
    }
    // GET: Editar Serie (Director)
    public ActionResult EditDirector(long? id)
    {
        if (!EsUsuarioPermitido() || (long)Session["UserType"] != 6 || id == null)
        {
            TempData["ErrorMessage"] = "No tiene permisos para editar esta serie";
            return RedirectToAction("ManageSeriesDirector");
        }

        long userId = (long)Session["UserId"];
        var director = db.Directors.FirstOrDefault(d => d.Id_Usuario == userId);
        if (director == null)
        {
            TempData["ErrorMessage"] = "No tiene un perfil de director válido";
            return RedirectToAction("ManageSeriesDirector");
        }

        var serie = db.Series
            .Include(s => s.Categoria)
            .Include(s => s.Clasificacion)
            .Include(s => s.Productor)
            .FirstOrDefault(s => s.Id_Serie == id && s.Id_Director == director.Id_Director);

        if (serie == null)
        {
            TempData["ErrorMessage"] = "Serie no encontrada o no tiene permisos para editarla";
            return HttpNotFound();
        }

        // Cargar listas con los datos actuales
        ViewBag.Id_Categoria = new SelectList(
            db.Categorias.AsNoTracking().OrderBy(c => c.Nombre).ToList(),
            "Id_Categoria", "Nombre", serie.Id_Categoria);

        ViewBag.Id_Clasificacion = new SelectList(
            db.Clasificacions.AsNoTracking().OrderBy(c => c.Nombre).ToList(),
            "Id_Clasificacion", "Nombre", serie.Id_Clasificacion);

        // Productores para directores
        var productores = db.Productors
            .AsNoTracking()
            .OrderBy(p => p.Nombre)
            .Select(p => new SelectListItem
            {
                Value = p.Id_Productor.ToString(),
                Text = p.Nombre,
                Selected = serie.Id_Productor == p.Id_Productor
            }).ToList();

        productores.Insert(0, new SelectListItem { Value = "", Text = "Seleccione productor..." });
        ViewBag.Id_Productor = productores;

        return View(serie);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public ActionResult EditProductor(Serie serie, HttpPostedFileBase imagen)
    {
        if (!EsUsuarioPermitido() || (long)Session["UserType"] != 5)
        {
            TempData["ErrorMessage"] = "Acceso denegado. Solo los productores pueden editar series.";
            return RedirectToAction("Index", "Home");
        }

        long userId = (long)Session["UserId"];
        var productor = db.Productors.FirstOrDefault(p => p.Id_Usuario == userId);

        if (productor == null)
        {
            TempData["ErrorMessage"] = "No tiene un perfil de productor válido";
            return RedirectToAction("CompleteProfile", "Account");
        }

        var serieOriginal = db.Series.AsNoTracking()
            .FirstOrDefault(s => s.Id_Serie == serie.Id_Serie && s.Id_Productor == productor.Id_Productor);

        if (serieOriginal == null)
        {
            TempData["ErrorMessage"] = "Serie no encontrada o no tiene permisos para editarla";
            return HttpNotFound();
        }

        if (ModelState.IsValid)
        {
            try
            {
                serie.Id_Productor = productor.Id_Productor;
                serie.Id_TipoContenido = serieOriginal.Id_TipoContenido;

                if (imagen != null && imagen.ContentLength > 0)
                {
                    EliminarImagenAnterior(serieOriginal.ImgURL);
                    serie.ImgURL = GuardarImagenSerie(imagen);
                }
                else
                {
                    serie.ImgURL = serieOriginal.ImgURL;
                }

                db.Entry(serie).State = EntityState.Modified;
                db.SaveChanges();

                TempData["SuccessMessage"] = "Serie actualizada exitosamente";
                return RedirectToAction("ManageSeriesProductor");
            }
            catch (DbEntityValidationException ex)
            {
                foreach (var eve in ex.EntityValidationErrors)
                {
                    foreach (var ve in eve.ValidationErrors)
                    {
                        ModelState.AddModelError(ve.PropertyName, ve.ErrorMessage);
                    }
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Error al guardar los cambios: " + ex.Message);
            }
        }

        CargarListasParaSerie(serie);
        return View(serie);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public ActionResult EditDirector(Serie serie, HttpPostedFileBase imagen)
    {
        if (!EsUsuarioPermitido() || (long)Session["UserType"] != 6)
        {
            TempData["ErrorMessage"] = "No tiene permisos para realizar esta acción";
            return RedirectToAction("ManageSeriesDirector");
        }

        long userId = (long)Session["UserId"];
        var director = db.Directors.FirstOrDefault(d => d.Id_Usuario == userId);
        if (director == null)
        {
            TempData["ErrorMessage"] = "No tiene un perfil de director válido";
            return RedirectToAction("ManageSeriesDirector");
        }

        var serieOriginal = db.Series.AsNoTracking()
            .FirstOrDefault(s => s.Id_Serie == serie.Id_Serie && s.Id_Director == director.Id_Director);

        if (serieOriginal == null)
        {
            TempData["ErrorMessage"] = "Serie no encontrada o no tiene permisos para editarla";
            return HttpNotFound();
        }

        if (ModelState.IsValid)
        {
            try
            {
                serie.Id_Director = director.Id_Director;
                serie.Id_TipoContenido = serieOriginal.Id_TipoContenido;

                if (imagen != null && imagen.ContentLength > 0)
                {
                    EliminarImagenAnterior(serieOriginal.ImgURL);
                    serie.ImgURL = GuardarImagenSerie(imagen);
                }
                else
                {
                    serie.ImgURL = serieOriginal.ImgURL;
                }

                db.Entry(serie).State = EntityState.Modified;
                db.SaveChanges();

                TempData["SuccessMessage"] = "Serie actualizada exitosamente";
                return RedirectToAction("ManageSeriesDirector");
            }
            catch (DbEntityValidationException ex)
            {
                foreach (var error in ex.EntityValidationErrors)
                {
                    foreach (var validationError in error.ValidationErrors)
                    {
                        ModelState.AddModelError(validationError.PropertyName, validationError.ErrorMessage);
                    }
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Ocurrió un error al guardar los cambios: " + ex.Message);
            }
        }

        // Recargar las listas si hay errores
        ViewBag.Id_Categoria = new SelectList(
            db.Categorias.AsNoTracking().OrderBy(c => c.Nombre).ToList(),
            "Id_Categoria", "Nombre", serie.Id_Categoria);

        ViewBag.Id_Clasificacion = new SelectList(
            db.Clasificacions.AsNoTracking().OrderBy(c => c.Nombre).ToList(),
            "Id_Clasificacion", "Nombre", serie.Id_Clasificacion);

        var productores = db.Productors
            .AsNoTracking()
            .OrderBy(p => p.Nombre)
            .Select(p => new SelectListItem
            {
                Value = p.Id_Productor.ToString(),
                Text = p.Nombre,
                Selected = serie.Id_Productor == p.Id_Productor
            }).ToList();

        productores.Insert(0, new SelectListItem { Value = "", Text = "Seleccione productor..." });
        ViewBag.Id_Productor = productores;

        return View(serie);
    }

    // GET: Eliminar Serie (Productor)
    public ActionResult DeleteProductor(long? id)
    {
        if (!EsUsuarioPermitido() || (long)Session["UserType"] != 5 || id == null)
        {
            TempData["ErrorMessage"] = "Acceso denegado. Solo los productores pueden eliminar series.";
            return RedirectToAction("ManageSeriesProductor");
        }

        long userId = (long)Session["UserId"];
        var productor = db.Productors.FirstOrDefault(p => p.Id_Usuario == userId);

        if (productor == null)
        {
            TempData["ErrorMessage"] = "No tiene un perfil de productor válido";
            return RedirectToAction("CompleteProfile", "Account");
        }

        var serie = db.Series
            .Include(s => s.Categoria)
            .Include(s => s.Clasificacion)
            .Include(s => s.Director)
            .FirstOrDefault(s => s.Id_Serie == id && s.Id_Productor == productor.Id_Productor);

        if (serie == null)
        {
            TempData["ErrorMessage"] = "Serie no encontrada o no tiene permisos para eliminarla";
            return HttpNotFound();
        }

        return View(serie);
    }

    // GET: Eliminar Serie (Director)
    public ActionResult DeleteDirector(long? id)
    {
        if (!EsUsuarioPermitido() || (long)Session["UserType"] != 6 || id == null)
        {
            TempData["ErrorMessage"] = "No tiene permisos para realizar esta acción";
            return RedirectToAction("ManageSeriesDirector");
        }

        long userId = (long)Session["UserId"];
        var director = db.Directors.FirstOrDefault(d => d.Id_Usuario == userId);
        if (director == null)
        {
            TempData["ErrorMessage"] = "No tiene un perfil de director válido";
            return RedirectToAction("ManageSeriesDirector");
        }

        var serie = db.Series
            .Include(s => s.Categoria)
            .Include(s => s.Clasificacion)
            .Include(s => s.Productor)
            .FirstOrDefault(s => s.Id_Serie == id && s.Id_Director == director.Id_Director);

        if (serie == null)
        {
            TempData["ErrorMessage"] = "Serie no encontrada o no tiene permisos para eliminarla";
            return HttpNotFound();
        }

        return View(serie);
    }

    [HttpPost, ActionName("DeleteProductor")]
    [ValidateAntiForgeryToken]
    public ActionResult DeleteConfirmedProductor(long id)
    {
        if (!EsUsuarioPermitido() || (long)Session["UserType"] != 5)
        {
            TempData["ErrorMessage"] = "Acceso denegado. Solo los productores pueden eliminar series.";
            return RedirectToAction("ManageSeriesProductor");
        }

        using (var transaction = db.Database.BeginTransaction())
        {
            try
            {
                long userId = (long)Session["UserId"];
                var productor = db.Productors.FirstOrDefault(p => p.Id_Usuario == userId);

                if (productor == null)
                {
                    TempData["ErrorMessage"] = "No tiene un perfil de productor válido";
                    return RedirectToAction("ManageSeriesProductor");
                }

                // Cargar la serie con todas sus relaciones
                var serie = db.Series
                    .Include(s => s.Contenidoes)
                    .Include(s => s.Temporadas.Select(t => t.Capituloes))
                    .FirstOrDefault(s => s.Id_Serie == id && s.Id_Productor == productor.Id_Productor);

                if (serie == null)
                {
                    TempData["ErrorMessage"] = "Serie no encontrada o no tiene permisos para eliminarla";
                    return HttpNotFound();
                }

                // 1. Eliminar todos los capítulos de todas las temporadas
                foreach (var temporada in serie.Temporadas.ToList())
                {
                    db.Capituloes.RemoveRange(temporada.Capituloes.ToList());
                }

                // 2. Eliminar todas las temporadas
                db.Temporadas.RemoveRange(serie.Temporadas.ToList());

                // 3. Eliminar contenido relacionado
                db.Contenidoes.RemoveRange(serie.Contenidoes.ToList());

                // 4. Eliminar la serie
                db.Series.Remove(serie);

                // Guardar todos los cambios
                db.SaveChanges();

                // 5. Eliminar la imagen asociada (si no es la imagen por defecto)
                EliminarImagenAnterior(serie.ImgURL);

                // Confirmar la transacción
                transaction.Commit();

                TempData["SuccessMessage"] = "Serie eliminada exitosamente";
                return RedirectToAction("ManageSeriesProductor");
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                System.Diagnostics.Debug.WriteLine($"Error al eliminar serie: {ex}");
                TempData["ErrorMessage"] = $"Error al eliminar la serie: {ex.Message}";
                return RedirectToAction("DeleteProductor", new { id = id });
            }
        }
    }

    [HttpPost, ActionName("DeleteDirector")]
    [ValidateAntiForgeryToken]
    public ActionResult DeleteConfirmedDirector(long id)
    {
        if (!EsUsuarioPermitido() || (long)Session["UserType"] != 6)
        {
            TempData["ErrorMessage"] = "No tiene permisos para realizar esta acción";
            return RedirectToAction("ManageSeriesDirector");
        }

        using (var transaction = db.Database.BeginTransaction())
        {
            try
            {
                long userId = (long)Session["UserId"];
                var director = db.Directors.FirstOrDefault(d => d.Id_Usuario == userId);
                if (director == null)
                {
                    TempData["ErrorMessage"] = "No tiene un perfil de director válido";
                    return RedirectToAction("ManageSeriesDirector");
                }

                // Cargar la serie con todas sus relaciones
                var serie = db.Series
                    .Include(s => s.Contenidoes)
                    .Include(s => s.Temporadas.Select(t => t.Capituloes))
                    .FirstOrDefault(s => s.Id_Serie == id && s.Id_Director == director.Id_Director);

                if (serie == null)
                {
                    TempData["ErrorMessage"] = "Serie no encontrada o no tiene permisos para eliminarla";
                    return HttpNotFound();
                }

                // 1. Eliminar todos los capítulos de todas las temporadas
                foreach (var temporada in serie.Temporadas.ToList())
                {
                    db.Capituloes.RemoveRange(temporada.Capituloes.ToList());
                }

                // 2. Eliminar todas las temporadas
                db.Temporadas.RemoveRange(serie.Temporadas.ToList());

                // 3. Eliminar contenido relacionado
                db.Contenidoes.RemoveRange(serie.Contenidoes.ToList());

                // 4. Eliminar la serie
                db.Series.Remove(serie);

                // Guardar todos los cambios
                db.SaveChanges();

                // 5. Eliminar la imagen asociada (si no es la imagen por defecto)
                EliminarImagenAnterior(serie.ImgURL);

                // Confirmar la transacción
                transaction.Commit();

                TempData["SuccessMessage"] = "Serie eliminada exitosamente";
                return RedirectToAction("ManageSeriesDirector");
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                System.Diagnostics.Debug.WriteLine($"Error al eliminar serie: {ex}");
                TempData["ErrorMessage"] = $"Error al eliminar la serie: {ex.Message}";
                return RedirectToAction("DeleteDirector", new { id = id });
            }
        }
    }

    // Gestión de Temporadas y Capítulos
    public ActionResult ManageTemporadas(long idSerie)
    {
        if (!EsUsuarioPermitido())
        {
            TempData["ErrorMessage"] = "Acceso denegado";
            return RedirectToAction("Index", "Home");
        }

        var serie = db.Series
            .Include(s => s.Temporadas)
            .FirstOrDefault(s => s.Id_Serie == idSerie);

        if (serie == null)
        {
            TempData["ErrorMessage"] = "Serie no encontrada";
            return HttpNotFound();
        }

        // Verificar permisos según tipo de usuario
        long userId = (long)Session["UserId"];
        long userType = (long)Session["UserType"];

        if (userType == 5) // Productor
        {
            var productor = db.Productors.FirstOrDefault(p => p.Id_Usuario == userId);
            if (productor == null || serie.Id_Productor != productor.Id_Productor)
            {
                TempData["ErrorMessage"] = "No tiene permisos para gestionar esta serie";
                return RedirectToAction("ManageSeriesProductor");
            }
        }
        else if (userType == 6) // Director
        {
            var director = db.Directors.FirstOrDefault(d => d.Id_Usuario == userId);
            if (director == null || serie.Id_Director != director.Id_Director)
            {
                TempData["ErrorMessage"] = "No tiene permisos para gestionar esta serie";
                return RedirectToAction("ManageSeriesDirector");
            }
        }
        else
        {
            TempData["ErrorMessage"] = "Acceso no autorizado";
            return RedirectToAction("Index", "Home");
        }

        ViewBag.SerieNombre = serie.Nombre;
        ViewBag.IdSerie = serie.Id_Serie;

        return View(serie.Temporadas.OrderBy(t => t.Nombre).ToList());
    }

    // GET: Crear Temporada
    public ActionResult CreateTemporada(long idSerie)
    {
        if (!EsUsuarioPermitido())
        {
            TempData["ErrorMessage"] = "Acceso denegado";
            return RedirectToAction("Index", "Home");
        }

        var serie = db.Series.Find(idSerie);
        if (serie == null)
        {
            TempData["ErrorMessage"] = "Serie no encontrada";
            return HttpNotFound();
        }

        // Verificar permisos
        if (!VerificarPermisosSerie(serie))
        {
            TempData["ErrorMessage"] = "No tiene permisos para gestionar esta serie";
            return RedirectToAction("Index", "Home");
        }

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
            return RedirectToAction("Index", "Home");
        }

        var serie = db.Series.Find(temporada.Id_Serie);
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

        if (ModelState.IsValid)
        {
            try
            {
                db.Temporadas.Add(temporada);
                db.SaveChanges();
                TempData["SuccessMessage"] = "Temporada creada exitosamente";
                return RedirectToAction("ManageTemporadas", new { idSerie = temporada.Id_Serie });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Error al guardar: " + ex.Message);
            }
        }

        return View(temporada);
    }

    // GET: Editar Temporada
    public ActionResult EditTemporada(long? id)
    {
        if (!EsUsuarioPermitido() || id == null)
        {
            TempData["ErrorMessage"] = "Acceso denegado";
            return RedirectToAction("Index", "Home");
        }

        var temporada = db.Temporadas.Find(id);
        if (temporada == null)
        {
            TempData["ErrorMessage"] = "Temporada no encontrada";
            return HttpNotFound();
        }

        var serie = db.Series.Find(temporada.Id_Serie);
        if (!VerificarPermisosSerie(serie))
        {
            TempData["ErrorMessage"] = "No tiene permisos para gestionar esta serie";
            return RedirectToAction("Index", "Home");
        }

        return View(temporada);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public ActionResult EditTemporada(Temporada temporada)
    {
        if (!EsUsuarioPermitido())
        {
            TempData["ErrorMessage"] = "Acceso denegado";
            return RedirectToAction("Index", "Home");
        }

        var tempOriginal = db.Temporadas.AsNoTracking().FirstOrDefault(t => t.Id_Temporada == temporada.Id_Temporada);
        if (tempOriginal == null)
        {
            TempData["ErrorMessage"] = "Temporada no encontrada";
            return HttpNotFound();
        }

        var serie = db.Series.Find(tempOriginal.Id_Serie);
        if (!VerificarPermisosSerie(serie))
        {
            TempData["ErrorMessage"] = "No tiene permisos para gestionar esta serie";
            return RedirectToAction("Index", "Home");
        }

        if (ModelState.IsValid)
        {
            try
            {
                db.Entry(temporada).State = EntityState.Modified;
                db.SaveChanges();
                TempData["SuccessMessage"] = "Temporada actualizada exitosamente";
                return RedirectToAction("ManageTemporadas", new { idSerie = tempOriginal.Id_Serie });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Error al guardar: " + ex.Message);
            }
        }

        return View(temporada);
    }

    // GET: Eliminar Temporada
    public ActionResult DeleteTemporada(long? id)
    {
        if (!EsUsuarioPermitido() || id == null)
        {
            TempData["ErrorMessage"] = "Acceso denegado";
            return RedirectToAction("Index", "Home");
        }

        var temporada = db.Temporadas.Find(id);
        if (temporada == null)
        {
            TempData["ErrorMessage"] = "Temporada no encontrada";
            return HttpNotFound();
        }

        var serie = db.Series.Find(temporada.Id_Serie);
        if (!VerificarPermisosSerie(serie))
        {
            TempData["ErrorMessage"] = "No tiene permisos para gestionar esta serie";
            return RedirectToAction("Index", "Home");
        }

        return View(temporada);
    }

    [HttpPost, ActionName("DeleteTemporada")]
    [ValidateAntiForgeryToken]
    public ActionResult DeleteConfirmedTemporada(long id)
    {
        if (!EsUsuarioPermitido())
        {
            TempData["ErrorMessage"] = "Acceso denegado";
            return RedirectToAction("Index", "Home");
        }

        using (var transaction = db.Database.BeginTransaction())
        {
            try
            {
                var temporada = db.Temporadas
                    .Include(t => t.Capituloes)
                    .FirstOrDefault(t => t.Id_Temporada == id);

                if (temporada == null)
                {
                    TempData["ErrorMessage"] = "Temporada no encontrada";
                    return HttpNotFound();
                }

                var serie = db.Series.Find(temporada.Id_Serie);
                if (!VerificarPermisosSerie(serie))
                {
                    TempData["ErrorMessage"] = "No tiene permisos para gestionar esta serie";
                    return RedirectToAction("Index", "Home");
                }

                // Eliminar capítulos relacionados
                if (temporada.Capituloes != null && temporada.Capituloes.Any())
                {
                    db.Capituloes.RemoveRange(temporada.Capituloes);
                }

                // Eliminar temporada
                db.Temporadas.Remove(temporada);
                db.SaveChanges();

                transaction.Commit();

                TempData["SuccessMessage"] = "Temporada eliminada exitosamente";
                return RedirectToAction("ManageTemporadas", new { idSerie = temporada.Id_Serie });
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                System.Diagnostics.Debug.WriteLine($"Error al eliminar temporada: {ex}");
                TempData["ErrorMessage"] = $"Error al eliminar la temporada: {ex.Message}";
                return RedirectToAction("DeleteTemporada", new { id = id });
            }
        }
    }

    // GET: Gestionar Capítulos
    public ActionResult ManageCapitulos(long idTemporada)
    {
        if (!EsUsuarioPermitido())
        {
            TempData["ErrorMessage"] = "Acceso denegado";
            return RedirectToAction("Index", "Home");
        }

        var temporada = db.Temporadas
            .Include(t => t.Capituloes)
            .Include(t => t.Serie)
            .FirstOrDefault(t => t.Id_Temporada == idTemporada);

        if (temporada == null)
        {
            TempData["ErrorMessage"] = "Temporada no encontrada";
            return HttpNotFound();
        }

        if (!VerificarPermisosSerie(temporada.Serie))
        {
            TempData["ErrorMessage"] = "No tiene permisos para gestionar esta serie";
            return RedirectToAction("Index", "Home");
        }

        ViewBag.TemporadaNombre = temporada.Nombre;
        ViewBag.SerieNombre = temporada.Serie.Nombre;
        ViewBag.IdTemporada = temporada.Id_Temporada;
        ViewBag.IdSerie = temporada.Serie.Id_Serie;

        return View(temporada.Capituloes.OrderBy(c => c.Nombre).ToList());
    }

    // GET: Crear Capítulo
    public ActionResult CreateCapitulo(long idTemporada)
    {
        if (!EsUsuarioPermitido())
        {
            TempData["ErrorMessage"] = "Acceso denegado";
            return RedirectToAction("Index", "Home");
        }

        var temporada = db.Temporadas
            .Include(t => t.Serie)
            .FirstOrDefault(t => t.Id_Temporada == idTemporada);

        if (temporada == null)
        {
            TempData["ErrorMessage"] = "Temporada no encontrada";
            return HttpNotFound();
        }

        if (!VerificarPermisosSerie(temporada.Serie))
        {
            TempData["ErrorMessage"] = "No tiene permisos para gestionar esta serie";
            return RedirectToAction("Index", "Home");
        }

        var capitulo = new Capitulo
        {
            Id_Temporada = idTemporada,
            Duracion = new TimeSpan(0, 45, 0) // Duración por defecto: 45 minutos
        };

        ViewBag.TemporadaNombre = temporada.Nombre;
        ViewBag.SerieNombre = temporada.Serie.Nombre;

        return View(capitulo);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public ActionResult CreateCapitulo(Capitulo capitulo)
    {
        if (!EsUsuarioPermitido())
        {
            TempData["ErrorMessage"] = "Acceso denegado";
            return RedirectToAction("Index", "Home");
        }

        var temporada = db.Temporadas
            .Include(t => t.Serie)
            .FirstOrDefault(t => t.Id_Temporada == capitulo.Id_Temporada);

        if (temporada == null)
        {
            TempData["ErrorMessage"] = "Temporada no encontrada";
            return HttpNotFound();
        }

        if (!VerificarPermisosSerie(temporada.Serie))
        {
            TempData["ErrorMessage"] = "No tiene permisos para gestionar esta serie";
            return RedirectToAction("Index", "Home");
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
            return RedirectToAction("Index", "Home");
        }

        ViewBag.TemporadaNombre = capitulo.Temporada.Nombre;
        ViewBag.SerieNombre = capitulo.Temporada.Serie.Nombre;

        return View(capitulo);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public ActionResult EditCapitulo(Capitulo capitulo)
    {
        if (!EsUsuarioPermitido())
        {
            TempData["ErrorMessage"] = "Acceso denegado";
            return RedirectToAction("Index", "Home");
        }

        var capOriginal = db.Capituloes.AsNoTracking()
            .Include(c => c.Temporada)
            .Include(c => c.Temporada.Serie)
            .FirstOrDefault(c => c.Id_Capitulo == capitulo.Id_Capitulo);

        if (capOriginal == null)
        {
            TempData["ErrorMessage"] = "Capítulo no encontrado";
            return HttpNotFound();
        }

        if (!VerificarPermisosSerie(capOriginal.Temporada.Serie))
        {
            TempData["ErrorMessage"] = "No tiene permisos para gestionar esta serie";
            return RedirectToAction("Index", "Home");
        }

        if (ModelState.IsValid)
        {
            try
            {
                db.Entry(capitulo).State = EntityState.Modified;
                db.SaveChanges();
                TempData["SuccessMessage"] = "Capítulo actualizado exitosamente";
                return RedirectToAction("ManageCapitulos", new { idTemporada = capOriginal.Id_Temporada });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Error al guardar: " + ex.Message);
            }
        }

        ViewBag.TemporadaNombre = capOriginal.Temporada.Nombre;
        ViewBag.SerieNombre = capOriginal.Temporada.Serie.Nombre;

        return View(capitulo);
    }

    // GET: Eliminar Capítulo
    public ActionResult DeleteCapitulo(long? id)
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
            return RedirectToAction("Index", "Home");
        }

        ViewBag.TemporadaNombre = capitulo.Temporada.Nombre;
        ViewBag.SerieNombre = capitulo.Temporada.Serie.Nombre;

        return View(capitulo);
    }

    [HttpPost, ActionName("DeleteCapitulo")]
    [ValidateAntiForgeryToken]
    public ActionResult DeleteConfirmedCapitulo(long id)
    {
        if (!EsUsuarioPermitido())
        {
            TempData["ErrorMessage"] = "Acceso denegado";
            return RedirectToAction("Index", "Home");
        }

        var capitulo = db.Capituloes
            .Include(c => c.Temporada)
            .FirstOrDefault(c => c.Id_Capitulo == id);

        if (capitulo == null)
        {
            TempData["ErrorMessage"] = "Capítulo no encontrado";
            return HttpNotFound();
        }

        var serie = db.Series.Find(capitulo.Temporada.Id_Serie);
        if (!VerificarPermisosSerie(serie))
        {
            TempData["ErrorMessage"] = "No tiene permisos para gestionar esta serie";
            return RedirectToAction("Index", "Home");
        }

        try
        {
            db.Capituloes.Remove(capitulo);
            db.SaveChanges();
            TempData["SuccessMessage"] = "Capítulo eliminado exitosamente";
            return RedirectToAction("ManageCapitulos", new { idTemporada = capitulo.Id_Temporada });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error al eliminar capítulo: {ex}");
            TempData["ErrorMessage"] = $"Error al eliminar el capítulo: {ex.Message}";
            return RedirectToAction("DeleteCapitulo", new { id = id });
        }
    }

    #region Métodos Auxiliares

    private void CargarListasParaSerie(Serie serie = null)
    {
        try
        {
            // Categorías
            var categorias = db.Categorias.AsNoTracking().OrderBy(c => c.Nombre).ToList();
            ViewBag.Id_Categoria = new SelectList(categorias, "Id_Categoria", "Nombre", serie?.Id_Categoria);

            // Clasificaciones
            var clasificaciones = db.Clasificacions.AsNoTracking().OrderBy(c => c.Nombre).ToList();
            ViewBag.Id_Clasificacion = new SelectList(clasificaciones, "Id_Clasificacion", "Nombre", serie?.Id_Clasificacion);

            // Directores para productores
            if (Session["UserType"] != null && (long)Session["UserType"] == 5)
            {
                var directores = db.Directors
                    .AsNoTracking()
                    .OrderBy(d => d.Nombre)
                    .Select(d => new
                    {
                        Value = d.Id_Director.ToString(),
                        Text = d.Nombre
                    }).ToList();

                ViewBag.Id_Director = new SelectList(directores, "Value", "Text", serie?.Id_Director);
            }

            // Productores para directores
            if (Session["UserType"] != null && (long)Session["UserType"] == 6)
            {
                var productores = db.Productors
                    .AsNoTracking()
                    .OrderBy(p => p.Nombre)
                    .Select(p => new
                    {
                        Value = p.Id_Productor.ToString(),
                        Text = p.Nombre
                    }).ToList();

                ViewBag.Id_Productor = new SelectList(productores, "Value", "Text", serie?.Id_Productor);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error cargando listas: {ex}");
            // Listas vacías como fallback
            ViewBag.Id_Categoria = new SelectList(new List<Categoria>(), "Id_Categoria", "Nombre");
            ViewBag.Id_Clasificacion = new SelectList(new List<Clasificacion>(), "Id_Clasificacion", "Nombre");
            ViewBag.Id_Director = new SelectList(new List<SelectListItem>(), "Value", "Text");
            ViewBag.Id_Productor = new SelectList(new List<SelectListItem>(), "Value", "Text");
        }
    }
    private string GuardarImagenSerie(HttpPostedFileBase imagen)
    {
        try
        {
            var extensionesPermitidas = new[] { ".jpg", ".jpeg", ".png", ".gif" };
            var extension = Path.GetExtension(imagen.FileName).ToLower();
            if (!extensionesPermitidas.Contains(extension))
            {
                return null;
            }

            var ruta = Server.MapPath("~/Content/Images/SeriesCovers");
            if (!Directory.Exists(ruta))
            {
                Directory.CreateDirectory(ruta);
            }

            var nombreArchivo = $"serie_{DateTime.Now:yyyyMMddHHmmssfff}{extension}";
            var rutaCompleta = Path.Combine(ruta, nombreArchivo);
            imagen.SaveAs(rutaCompleta);

            return "/Content/Images/SeriesCovers/" + nombreArchivo;
        }
        catch
        {
            return null;
        }
    }

    private void EliminarImagenAnterior(string imageUrl)
    {
        if (!string.IsNullOrEmpty(imageUrl) &&
            !imageUrl.Equals("/Content/Images/SeriesCovers/default.jpg", StringComparison.OrdinalIgnoreCase))
        {
            var rutaFisica = Server.MapPath(imageUrl);
            if (System.IO.File.Exists(rutaFisica))
            {
                try
                {
                    System.IO.File.Delete(rutaFisica);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error al eliminar imagen: {ex.Message}");
                }
            }
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

    #endregion

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            db?.Dispose();
        }
        base.Dispose(disposing);
    }
}