using System;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using PARCIALDBEveriflix.Models;
using System.Collections.Generic;
using System.Data.Entity.Validation;
using System.IO;
using System.Web;

public class PeliculaController : Controller
{
    private EveryflixDBEntities db = new EveryflixDBEntities();

    private bool EsUsuarioPermitido()
    {
        // Verificar que la sesión tiene los datos necesarios
        if (Session["UserId"] == null || Session["UserType"] == null)
        {
            System.Diagnostics.Debug.WriteLine("Error: Faltan datos de sesión (UserId o UserType)");
            return false;
        }

        try
        {
            long userId = (long)Session["UserId"];
            long userType = (long)Session["UserType"];

            // Verificar el tipo de usuario y su existencia en la tabla correspondiente
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

    // GET: Películas para Productores
    public ActionResult ManageMoviesProductor()
    {
        if (!EsUsuarioPermitido() || (long)Session["UserType"] != 5)
            return RedirectToAction("Index", "Home");

        long userId = (long)Session["UserId"];
        var peliculas = db.Peliculas
                        .Include(p => p.Categoria)
                        .Include(p => p.Clasificacion)
                        .Include(p => p.Director)
                        .Where(p => p.Id_Productor == userId)
                        .ToList();

        return View(peliculas);
    }

    // GET: Películas para Directores
    public ActionResult ManageMoviesDirector()
    {
        try
        {
            // 1. Verificar autenticación
            if (Session["UserId"] == null || Session["UserType"] == null)
            {
                TempData["ErrorMessage"] = "Debe iniciar sesión";
                return RedirectToAction("Login", "Account");
            }

            // 2. Verificar que es director
            if ((long)Session["UserType"] != 6) // 6 = Director
            {
                TempData["ErrorMessage"] = "Acceso no autorizado";
                return RedirectToAction("Index", "Home");
            }

            // 3. Obtener director asociado al usuario
            long userId = (long)Session["UserId"];
            var director = db.Directors
                            .Include(d => d.Peliculas) // Incluir películas
                            .FirstOrDefault(d => d.Id_Usuario == userId);

            if (director == null)
            {
                TempData["ErrorMessage"] = "Perfil de director no encontrado";
                return RedirectToAction("CompleteProfile", "Account");
            }

            // 4. Obtener películas con toda la información relacionada
            var peliculas = db.Peliculas
                .Include(p => p.Productor)
                .Include(p => p.Categoria)
                .Include(p => p.Clasificacion)
                .Where(p => p.Id_Director == director.Id_Director)
                .OrderBy(p => p.Nombre)
                .ToList();

            // 5. Pasar el nombre del director a la vista
            ViewBag.NombreDirector = director.Nombre;

            return View(peliculas);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error en ManageMoviesDirector: {ex}");
            TempData["ErrorMessage"] = "Error al cargar películas";
            return RedirectToAction("Index", "Home");
        }
    }
    // GET: Crear película (Productor)
    public ActionResult CreateProductor()
    {
        if (!EsUsuarioPermitido() || (long)Session["UserType"] != 5)
            return RedirectToAction("Index", "Home");

        CargarListasParaProductor();
        return View();
    }

    // GET: Crear película (Director)
    public ActionResult CreateDirector()
    {
        try
        {
            // 1. Verificar autenticación y permisos
            if (Session["UserId"] == null || Session["UserType"] == null)
            {
                TempData["ErrorMessage"] = "Debe iniciar sesión para acceder a esta función";
                return RedirectToAction("Login", "Account"); // Asegúrate que esta ruta existe
            }

            if ((long)Session["UserType"] != 6) // 6 = Director
            {
                TempData["ErrorMessage"] = "No tiene permisos para acceder a esta sección";
                return RedirectToAction("Index", "Home");
            }

            // 2. Verificar director asociado
            long userId = (long)Session["UserId"];
            var director = db.Directors.FirstOrDefault(d => d.Id_Usuario == userId);

            if (director == null)
            {
                TempData["ErrorMessage"] = "No tiene un perfil de director configurado";
                return RedirectToAction("CompleteProfile", "Account"); // Sugerir completar perfil
            }

            // 3. Cargar datos para los dropdowns
            try
            {
                // Categorías
                var categorias = db.Categorias.AsNoTracking().OrderBy(c => c.Nombre).ToList();
                if (!categorias.Any())
                {
                    System.Diagnostics.Debug.WriteLine("Advertencia: No se encontraron categorías");
                    TempData["WarningMessage"] = "No hay categorías disponibles. Contacte al administrador.";
                }
                ViewBag.Id_Categoria = new SelectList(categorias, "Id_Categoria", "Nombre");

                // Clasificaciones
                var clasificaciones = db.Clasificacions.AsNoTracking().OrderBy(c => c.Nombre).ToList();
                ViewBag.Id_Clasificacion = new SelectList(clasificaciones, "Id_Clasificacion", "Nombre");

                // Productores
                var productores = db.Productors
                    .AsNoTracking()
                    .OrderBy(p => p.Nombre)
                    .Select(p => new SelectListItem
                    {
                        Value = p.Id_Productor.ToString(),
                        Text = p.Nombre
                    }).ToList();

                ViewBag.Id_Productor = productores;

                return View();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al cargar listas: {ex}");
                TempData["ErrorMessage"] = "Error al cargar los datos del formulario";
                return RedirectToAction("Index", "Home");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error en CreateDirector: {ex}");
            TempData["ErrorMessage"] = "Ocurrió un error inesperado";
            return RedirectToAction("Index", "Home");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public ActionResult CreateProductor(Pelicula pelicula, HttpPostedFileBase imagen)
    {
        if (ModelState.IsValid)
        {
            try
            {
                // Asignar valores específicos
                pelicula.Id_Productor = (long)Session["UserId"];
                pelicula.Id_TipoContenido = db.TipoContenidoes.First(t => t.Nombre == "Pelicula").Id_TipoContenido;

                // Procesar imagen
                if (imagen != null && imagen.ContentLength > 0)
                {
                    pelicula.ImgURL = GuardarImagen(imagen);
                }
                else
                {
                    pelicula.ImgURL = "/Content/Images/MovieCovers/default.jpg";
                }

                db.Peliculas.Add(pelicula);
                db.SaveChanges();


                TempData["SuccessMessage"] = "Película creada exitosamente";
                return RedirectToAction("ManageMoviesProductor");
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
        }

        CargarListasParaProductor();
        return View(pelicula);
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public ActionResult CreateDirector(Pelicula pelicula, HttpPostedFileBase imagen)
    {
        // 1. Verificar ModelState primero
        if (!ModelState.IsValid)
        {
            CargarListasParaVista(pelicula);
            return View(pelicula);
        }

        try
        {
            // 2. Verificar sesión y permisos
            if (Session["UserId"] == null || (long)Session["UserType"] != 6)
            {
                TempData["ErrorMessage"] = "No tienes permisos para realizar esta acción";
                return RedirectToAction("Login", "Account");
            }

            // 3. Obtener director asociado
            long userId = (long)Session["UserId"];
            var director = db.Directors.FirstOrDefault(d => d.Id_Usuario == userId);

            if (director == null)
            {
                TempData["ErrorMessage"] = "No tienes un perfil de director válido. Completa tu perfil primero.";
                return RedirectToAction("CompleteProfile", "Account");
            }

            // 4. Verificar y obtener tipo de contenido "Pelicula"
            var tipoContenido = db.TipoContenidoes.FirstOrDefault(t => t.Nombre == "Pelicula");
            if (tipoContenido == null)
            {
                // Si no existe, crearlo (esto es un fallback, idealmente debería existir)
                tipoContenido = new TipoContenido { Nombre = "Pelicula" };
                db.TipoContenidoes.Add(tipoContenido);
                db.SaveChanges();
            }

            // 5. Asignar valores requeridos
            pelicula.Id_Director = director.Id_Director;
            pelicula.Id_TipoContenido = tipoContenido.Id_TipoContenido;

            // 6. Manejar imagen
            if (imagen != null && imagen.ContentLength > 0)
            {
                pelicula.ImgURL = GuardarImagen(imagen);
                if (pelicula.ImgURL == null)
                {
                    CargarListasParaVista(pelicula);
                    return View(pelicula);
                }
            }
            else
            {
                pelicula.ImgURL = "/Content/Images/MovieCovers/default.jpg";
            }

            // 7. Validar productor si fue seleccionado
            if (pelicula.Id_Productor.HasValue && pelicula.Id_Productor.Value > 0)
            {
                var productorExistente = db.Productors.Find(pelicula.Id_Productor.Value);
                if (productorExistente == null)
                {
                    ModelState.AddModelError("Id_Productor", "Productor no válido");
                    CargarListasParaVista(pelicula);
                    return View(pelicula);
                }
            }
            else
            {
                pelicula.Id_Productor = null;
            }

            // 8. Validar categoría y clasificación
            if (!db.Categorias.Any(c => c.Id_Categoria == pelicula.Id_Categoria))
            {
                ModelState.AddModelError("Id_Categoria", "Categoría no válida");
                CargarListasParaVista(pelicula);
                return View(pelicula);
            }

            if (!db.Clasificacions.Any(c => c.Id_Clasificacion == pelicula.Id_Clasificacion))
            {
                ModelState.AddModelError("Id_Clasificacion", "Clasificación no válida");
                CargarListasParaVista(pelicula);
                return View(pelicula);
            }

            // 9. Guardar en base de datos
            db.Peliculas.Add(pelicula);
            db.SaveChanges();

            TempData["SuccessMessage"] = "Película creada exitosamente!";
            return RedirectToAction("ManageMoviesDirector");
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
            CargarListasParaVista(pelicula);
            return View(pelicula);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error completo: {ex}");
            TempData["ErrorMessage"] = $"Error al guardar: {ex.InnerException?.Message ?? ex.Message}";
            return RedirectToAction("CreateDirector");
        }
    }
    // GET: Editar (Productor)
    public ActionResult EditProductor(long? id)
    {
        if (!EsUsuarioPermitido() || (long)Session["UserType"] != 5 || id == null)
            return RedirectToAction("ManageMoviesProductor");

        long userId = (long)Session["UserId"];
        var pelicula = db.Peliculas
            .Include(p => p.Categoria)
            .Include(p => p.Clasificacion)
            .Include(p => p.Director)
            .FirstOrDefault(p => p.Id_pelicula == id && p.Id_Productor == userId);

        if (pelicula == null)
            return HttpNotFound();

        CargarListasParaProductor(pelicula);
        return View(pelicula);
    }

    // GET: Editar (Director)
    public ActionResult EditDirector(long? id)
    {
        if (!EsUsuarioPermitido() || (long)Session["UserType"] != 6 || id == null)
            return RedirectToAction("ManageMoviesDirector");

        long userId = (long)Session["UserId"];
        var pelicula = db.Peliculas
            .Include(p => p.Categoria)
            .Include(p => p.Clasificacion)
            .Include(p => p.Productor)
            .FirstOrDefault(p => p.Id_pelicula == id && p.Id_Director == userId);

        if (pelicula == null)
            return HttpNotFound();

        CargarListasParaDirector(pelicula);
        return View(pelicula);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public ActionResult EditProductor(Pelicula pelicula, HttpPostedFileBase imagen)
    {
        if (ModelState.IsValid)
        {
            try
            {
                // Mantener valores que no deben cambiar
                pelicula.Id_Productor = (long)Session["UserId"];
                pelicula.Id_TipoContenido = db.TipoContenidoes.First(t => t.Nombre == "Pelicula").Id_TipoContenido;

                // Procesar imagen si se subió una nueva
                if (imagen != null && imagen.ContentLength > 0)
                {
                    EliminarImagenAnterior(pelicula.ImgURL);
                    pelicula.ImgURL = GuardarImagen(imagen);
                }

                db.Entry(pelicula).State = EntityState.Modified;
                db.SaveChanges();

                TempData["SuccessMessage"] = "Película actualizada exitosamente";
                return RedirectToAction("ManageMoviesProductor");
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
        }

        CargarListasParaProductor(pelicula);
        return View(pelicula);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public ActionResult EditDirector(Pelicula pelicula, HttpPostedFileBase imagen)
    {
        if (ModelState.IsValid)
        {
            try
            {
                // Mantener valores que no deben cambiar
                pelicula.Id_Director = (long)Session["UserId"];
                pelicula.Id_TipoContenido = db.TipoContenidoes.First(t => t.Nombre == "Pelicula").Id_TipoContenido;

                // Procesar imagen si se subió una nueva
                if (imagen != null && imagen.ContentLength > 0)
                {
                    EliminarImagenAnterior(pelicula.ImgURL);
                    pelicula.ImgURL = GuardarImagen(imagen);
                }

                db.Entry(pelicula).State = EntityState.Modified;
                db.SaveChanges();

                TempData["SuccessMessage"] = "Película actualizada exitosamente";
                return RedirectToAction("ManageMoviesDirector");
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
        }

        CargarListasParaDirector(pelicula);
        return View(pelicula);
    }

    // Eliminar (Productor)
    public ActionResult DeleteProductor(long? id)
    {
        if (!EsUsuarioPermitido() || (long)Session["UserType"] != 5 || id == null)
            return RedirectToAction("ManageMoviesProductor");

        long userId = (long)Session["UserId"];
        var pelicula = db.Peliculas.FirstOrDefault(p => p.Id_pelicula == id && p.Id_Productor == userId);

        if (pelicula == null)
            return HttpNotFound();

        return View(pelicula);
    }

    // Eliminar (Director)
    public ActionResult DeleteDirector(long? id)
    {
        if (!EsUsuarioPermitido() || (long)Session["UserType"] != 6 || id == null)
            return RedirectToAction("ManageMoviesDirector");

        long userId = (long)Session["UserId"];
        var pelicula = db.Peliculas.FirstOrDefault(p => p.Id_pelicula == id && p.Id_Director == userId);

        if (pelicula == null)
            return HttpNotFound();

        return View(pelicula);
    }

    [HttpPost, ActionName("DeleteProductor")]
    [ValidateAntiForgeryToken]
    public ActionResult DeleteConfirmedProductor(long id)
    {
        using (var transaction = db.Database.BeginTransaction())
        {
            try
            {
                long userId = (long)Session["UserId"];
                var pelicula = db.Peliculas.FirstOrDefault(p => p.Id_pelicula == id && p.Id_Productor == userId);

                if (pelicula != null)
                {
                    // Eliminar contenido relacionado
                    var contenido = db.Contenidoes.FirstOrDefault(c => c.Id_Pelicula == id);
                    if (contenido != null)
                        db.Contenidoes.Remove(contenido);

                    // Eliminar película
                    db.Peliculas.Remove(pelicula);
                    db.SaveChanges();

                    // Eliminar imagen
                    EliminarImagenAnterior(pelicula.ImgURL);

                    transaction.Commit();
                    TempData["SuccessMessage"] = "Película eliminada exitosamente";
                }
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                TempData["ErrorMessage"] = "Error al eliminar la película: " + ex.Message;
            }
        }

        return RedirectToAction("ManageMoviesProductor");
    }

    [HttpPost, ActionName("DeleteDirector")]
    [ValidateAntiForgeryToken]
    public ActionResult DeleteConfirmedDirector(long id)
    {
        using (var transaction = db.Database.BeginTransaction())
        {
            try
            {
                long userId = (long)Session["UserId"];
                var pelicula = db.Peliculas.FirstOrDefault(p => p.Id_pelicula == id && p.Id_Director == userId);

                if (pelicula != null)
                {
                    // Eliminar contenido relacionado
                    var contenido = db.Contenidoes.FirstOrDefault(c => c.Id_Pelicula == id);
                    if (contenido != null)
                        db.Contenidoes.Remove(contenido);

                    // Eliminar película
                    db.Peliculas.Remove(pelicula);
                    db.SaveChanges();

                    // Eliminar imagen
                    EliminarImagenAnterior(pelicula.ImgURL);

                    transaction.Commit();
                    TempData["SuccessMessage"] = "Película eliminada exitosamente";
                }
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                TempData["ErrorMessage"] = "Error al eliminar la película: " + ex.Message;
            }
        }

        return RedirectToAction("ManageMoviesDirector");
    }

    #region Métodos Auxiliares

    private void CargarListasParaProductor(Pelicula pelicula = null)
    {
        // Categorías y clasificaciones
        ViewBag.Id_Categoria = new SelectList(db.Categorias.ToList(), "Id_Categoria", "Nombre", pelicula?.Id_Categoria);
        ViewBag.Id_Clasificacion = new SelectList(db.Clasificacions.ToList(), "Id_Clasificacion", "Nombre", pelicula?.Id_Clasificacion);

        // Directores (para que el productor pueda asignar)
        var directores = db.Directors
            .Select(d => new SelectListItem
            {
                Value = d.Id_Director.ToString(),
                Text = d.Nombre,
                Selected = pelicula != null && pelicula.Id_Director == d.Id_Director
            })
            .ToList();

        directores.Insert(0, new SelectListItem { Value = "", Text = "No asignar director" });
        ViewBag.Id_Director = directores;
    }

    private void CargarListasParaDirector(Pelicula pelicula = null)
    {
        try
        {
            // Categorías
            ViewBag.Id_Categoria = new SelectList(
                db.Categorias.AsNoTracking().OrderBy(c => c.Nombre).ToList(),
                "Id_Categoria", "Nombre",
                pelicula?.Id_Categoria
            );

            // Clasificaciones
            ViewBag.Id_Clasificacion = new SelectList(
                db.Clasificacions.AsNoTracking().OrderBy(c => c.Nombre).ToList(),
                "Id_Clasificacion", "Nombre",
                pelicula?.Id_Clasificacion
            );

            // Productores
            var productores = db.Productors
                .AsNoTracking()
                .OrderBy(p => p.Nombre)
                .Select(p => new {
                    p.Id_Productor,
                    p.Nombre
                }).ToList();

            ViewBag.Id_Productor = new SelectList(
                productores,
                "Id_Productor", "Nombre",
                pelicula?.Id_Productor
            );
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("Error al cargar listas: " + ex.Message);

            // Listas vacías para evitar errores en la vista
            ViewBag.Id_Categoria = new SelectList(new List<Categoria>(), "Id_Categoria", "Nombre");
            ViewBag.Id_Clasificacion = new SelectList(new List<Clasificacion>(), "Id_Clasificacion", "Nombre");
            ViewBag.Id_Productor = new SelectList(new List<object>(), "Id_Productor", "Nombre");
        }
    }

    private string ProcesarImagen(HttpPostedFileBase imagen)
    {
        if (imagen == null || imagen.ContentLength == 0)
            return "/Content/Images/MovieCovers/default.jpg";

        try
        {
            // Validar extensión
            var extensionesValidas = new[] { ".jpg", ".jpeg", ".png", ".gif" };
            var extension = Path.GetExtension(imagen.FileName).ToLower();
            if (!extensionesValidas.Contains(extension))
                throw new Exception("Solo se permiten imágenes JPG, PNG o GIF");

            // Configurar rutas
            var rutaVirtual = "~/Content/Images/MovieCovers";
            var rutaFisica = Server.MapPath(rutaVirtual);

            // Crear directorio si no existe
            if (!Directory.Exists(rutaFisica))
                Directory.CreateDirectory(rutaFisica);

            // Generar nombre único
            var nombreArchivo = $"movie_{Guid.NewGuid()}{extension}";
            var rutaCompleta = Path.Combine(rutaFisica, nombreArchivo);

            // Guardar imagen
            imagen.SaveAs(rutaCompleta);
            return Url.Content($"{rutaVirtual}/{nombreArchivo}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("Error al procesar imagen: " + ex.Message);
            return "/Content/Images/MovieCovers/default.jpg";
        }
    }
    private string GuardarImagen(HttpPostedFileBase imagen)
    {
        try
        {
            // Validaciones
            if (imagen == null || imagen.ContentLength == 0)
                return "/Content/Images/MovieCovers/default.jpg";

            var extensionesPermitidas = new[] { ".jpg", ".jpeg", ".png", ".gif" };
            var extension = Path.GetExtension(imagen.FileName).ToLower();

            if (!extensionesPermitidas.Contains(extension))
            {
                ModelState.AddModelError("ImgURL", "Formato de imagen no válido");
                return null;
            }

            // Configurar rutas
            var ruta = Server.MapPath("~/Content/Images/MovieCovers");
            if (!Directory.Exists(ruta))
                Directory.CreateDirectory(ruta);

            // Guardar archivo
            var nombreArchivo = $"pelicula_{DateTime.Now:yyyyMMddHHmmssfff}{extension}";
            var rutaCompleta = Path.Combine(ruta, nombreArchivo);
            imagen.SaveAs(rutaCompleta);

            return "/Content/Images/MovieCovers/" + nombreArchivo;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error al guardar imagen: {ex}");
            ModelState.AddModelError("ImgURL", "Error al guardar la imagen");
            return null;
        }
    }

    private void EliminarImagenAnterior(string imageUrl)
    {
        if (!string.IsNullOrEmpty(imageUrl) && !imageUrl.Equals("/Content/Images/MovieCovers/default.jpg", StringComparison.OrdinalIgnoreCase))
        {
            var rutaFisica = Server.MapPath(imageUrl);
            if (System.IO.File.Exists(rutaFisica))
                System.IO.File.Delete(rutaFisica);
        }
    }


    private void CargarListasCompletas(Pelicula pelicula = null)
    {
        // Categorías
        ViewBag.Id_Categoria = new SelectList(
            db.Categorias.OrderBy(c => c.Nombre).ToList(),
            "Id_Categoria",
            "Nombre",
            pelicula?.Id_Categoria
        );

        // Clasificaciones
        ViewBag.Id_Clasificacion = new SelectList(
            db.Clasificacions.OrderBy(c => c.Nombre).ToList(),
            "Id_Clasificacion",
            "Nombre",
            pelicula?.Id_Clasificacion
        );

        // Productores (para directores)
        var productores = db.Productors
            .OrderBy(p => p.Nombre)
            .Select(p => new SelectListItem
            {
                Value = p.Id_Productor.ToString(),
                Text = p.Nombre,
                Selected = pelicula != null && pelicula.Id_Productor == p.Id_Productor
            })
            .ToList();

        productores.Insert(0, new SelectListItem { Value = "", Text = "Seleccione un productor" });
        ViewBag.Id_Productor = productores;

        // Directores (para productores)
        var directores = db.Directors
            .OrderBy(d => d.Nombre)
            .Select(d => new SelectListItem
            {
                Value = d.Id_Director.ToString(),
                Text = d.Nombre,
                Selected = pelicula != null && pelicula.Id_Director == d.Id_Director
            })
            .ToList();

        directores.Insert(0, new SelectListItem { Value = "", Text = "Seleccione un director" });
        ViewBag.Id_Director = directores;
    }

    // Método unificado para cargar listas
    private void CargarListasParaVista(Pelicula pelicula = null)
    {
        try
        {
            ViewBag.Id_Categoria = new SelectList(
                db.Categorias.OrderBy(c => c.Nombre).ToList(),
                "Id_Categoria", "Nombre", pelicula?.Id_Categoria);

            ViewBag.Id_Clasificacion = new SelectList(
                db.Clasificacions.OrderBy(c => c.Nombre).ToList(),
                "Id_Clasificacion", "Nombre", pelicula?.Id_Clasificacion);

            var productores = db.Productors
                .OrderBy(p => p.Nombre)
                .Select(p => new SelectListItem
                {
                    Value = p.Id_Productor.ToString(),
                    Text = p.Nombre,
                    Selected = pelicula != null && pelicula.Id_Productor == p.Id_Productor
                }).ToList();

            productores.Insert(0, new SelectListItem { Value = "", Text = "Seleccione productor..." });
            ViewBag.Id_Productor = productores;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error cargando listas: {ex}");
            ViewBag.Id_Categoria = new SelectList(new List<Categoria>(), "Id_Categoria", "Nombre");
            ViewBag.Id_Clasificacion = new SelectList(new List<Clasificacion>(), "Id_Clasificacion", "Nombre");
            ViewBag.Id_Productor = new SelectList(new List<SelectListItem>(), "Value", "Text");
        }
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