using PARCIALDBEveriflix.Models;
using System;
using System.Linq;
using System.Web.Mvc;
using System.Data.Entity;
using System.Collections.Generic;

namespace PARCIALDBEveriflix.Controllers
{
    public class VotacionController : Controller
    {
        private EveryflixDBEntities db = new EveryflixDBEntities();

        // GET: Votacion/Index
        public ActionResult Index()
        {
            Dictionary<long, Voto> votosActuales = new Dictionary<long, Voto>();
            if (Session["UserId"] != null)
            {
                var userId = long.Parse(Session["UserId"].ToString());
                votosActuales = db.Votoes
                    .Where(v => v.Id_Usuario == userId)
                    .ToDictionary(v => v.Id_VotacionContenido, v => v);
            }

            ViewBag.VotosActuales = votosActuales;
            // Verificar si el usuario está logueado
            if (Session["UserId"] == null)
            {
                return RedirectToAction("Login", "Auth", new { returnUrl = Request.Url.PathAndQuery });
            }

            var votaciones = db.VotacionContenidoes
                .Include(v => v.TipoContenido)
                .Include(v => v.Contenido)
                .Include(v => v.Contenido1)
                .Include(v => v.Contenido2)
                .Include(v => v.Contenido3)
                .OrderByDescending(v => v.Id_VotacionContenido)
                .ToList();

            // Obtener conteo de votos
            var votosPorVotacion = db.Votoes
                .GroupBy(v => v.Id_VotacionContenido)
                .ToDictionary(
                    g => g.Key,
                    g => g.GroupBy(v => v.Id_Contenido)
                          .ToDictionary(
                              g2 => g2.Key,
                              g2 => g2.Count()
                          )
                );

            // Obtener votaciones en las que ya participó el usuario actual
            List<long> votacionesUsuario = new List<long>();
            if (Session["UserId"] != null)
            {
                var userId = long.Parse(Session["UserId"].ToString());
                votacionesUsuario = db.Votoes
                    .Where(v => v.Id_Usuario == userId)
                    .Select(v => v.Id_VotacionContenido)
                    .Distinct()
                    .ToList();
            }

            ViewBag.VotosPorVotacion = votosPorVotacion;
            ViewBag.VotosUsuario = votacionesUsuario;

            return View(votaciones);
        }

        // GET: Votacion/Create (Solo para admin)
        [HttpGet]
        public ActionResult Create()
        {
            // Verificar si es admin (tipo usuario 1)
            if (Session["UserType"] == null || Session["UserType"].ToString() != "1")
            {
                TempData["Error"] = "Acceso denegado. Se requieren permisos de administrador";
                return RedirectToAction("Index");
            }

            ViewBag.Id_TipoContenido = new SelectList(db.TipoContenidoes, "Id_TipoContenido", "Nombre");
            return View();
        }

        // POST: Votacion/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(VotacionContenido votacion, long[] contenidoIds)
        {
            // Verificar permisos de admin
            if (Session["UserType"] == null || Session["UserType"].ToString() != "1")
            {
                TempData["Error"] = "Acceso denegado. Se requieren permisos de administrador";
                return RedirectToAction("Index");
            }

            if (ModelState.IsValid)
            {
                if (contenidoIds == null || contenidoIds.Length < 2 || contenidoIds.Length > 4)
                {
                    ModelState.AddModelError("", "Debes seleccionar entre 2 y 4 contenidos para la votación");
                    ViewBag.Id_TipoContenido = new SelectList(db.TipoContenidoes, "Id_TipoContenido", "Nombre", votacion.Id_TipoContenido);
                    return View(votacion);
                }

                // Validar que todos los contenidos sean del mismo tipo
                var contenidos = db.Contenidoes
                    .Where(c => contenidoIds.Contains(c.Id_Contenido))
                    .ToList();

                var tipos = contenidos.Select(c =>
                    c.Pelicula != null ? 4L :
                    c.Serie != null ? 3L :
                    c.Cancion != null ? 2L : 0L)
                    .Distinct()
                    .ToList();

                if (tipos.Count != 1)
                {
                    ModelState.AddModelError("", "Todos los contenidos deben ser del mismo tipo");
                    ViewBag.Id_TipoContenido = new SelectList(db.TipoContenidoes, "Id_TipoContenido", "Nombre", votacion.Id_TipoContenido);
                    return View(votacion);
                }

                votacion.Id_TipoContenido = tipos.First();

                // Asignar los contenidos seleccionados
                if (contenidoIds.Length > 0) votacion.Id_Contenido1 = contenidoIds[0];
                if (contenidoIds.Length > 1) votacion.Id_Contenido2 = contenidoIds[1];
                if (contenidoIds.Length > 2) votacion.Id_Contenido3 = contenidoIds[2];
                if (contenidoIds.Length > 3) votacion.Id_Contenido4 = contenidoIds[3];

                db.VotacionContenidoes.Add(votacion);
                db.SaveChanges();

                TempData["Success"] = "Votación creada exitosamente";
                return RedirectToAction("Index");
            }

            ViewBag.Id_TipoContenido = new SelectList(db.TipoContenidoes, "Id_TipoContenido", "Nombre", votacion.Id_TipoContenido);
            return View(votacion);
        }

        // POST: Votacion/Votar/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Votar(long id, long contenidoId)
        {
            if (Session["UserId"] == null)
            {
                TempData["Error"] = "Debes iniciar sesión para votar";
                return RedirectToAction("Login", "Auth", new { returnUrl = Request.Url.PathAndQuery });
            }

            try
            {
                var userId = long.Parse(Session["UserId"].ToString());

                // Verificar si el usuario ya votó en esta votación
                var votoExistente = db.Votoes.FirstOrDefault(v => v.Id_VotacionContenido == id && v.Id_Usuario == userId);

                // Verificar que el contenido pertenece a la votación
                var votacion = db.VotacionContenidoes.Find(id);
                if (votacion == null || !ContenidoPerteneceAVotacion(votacion, contenidoId))
                {
                    TempData["Error"] = "Opción de voto no válida";
                    return RedirectToAction("Index");
                }

                if (votoExistente != null)
                {
                    // Si ya votó pero está seleccionando el mismo contenido
                    if (votoExistente.Id_Contenido == contenidoId)
                    {
                        TempData["Info"] = "Ya has votado por esta opción";
                        return RedirectToAction("Index");
                    }

                    // Cambiar el voto existente
                    votoExistente.Id_Contenido = contenidoId;
                    db.Entry(votoExistente).State = EntityState.Modified;
                    TempData["Success"] = "¡Has cambiado tu voto exitosamente!";
                }
                else
                {
                    // Registrar nuevo voto
                    var voto = new Voto
                    {
                        Id_Usuario = userId,
                        Id_Contenido = contenidoId,
                        Id_VotacionContenido = id,
                      
                    };
                    db.Votoes.Add(voto);
                    TempData["Success"] = "¡Tu voto ha sido registrado!";
                }

                db.SaveChanges();
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Ocurrió un error al registrar tu voto: " + ex.Message;
            }

            return RedirectToAction("Index");
        }
        // GET: Votacion/Resultados/5
        public ActionResult Resultados(long id)
        {
            var votacion = db.VotacionContenidoes
                .Include(v => v.TipoContenido)
                .Include(v => v.Contenido)
                .Include(v => v.Contenido1)
                .Include(v => v.Contenido2)
                .Include(v => v.Contenido3)
                .FirstOrDefault(v => v.Id_VotacionContenido == id);

            if (votacion == null)
            {
                return HttpNotFound();
            }

            // Obtener el conteo de votos
            var votos = db.Votoes
                .Where(v => v.Id_VotacionContenido == id)
                .GroupBy(v => v.Id_Contenido)
                .Select(g => new {
                    ContenidoId = g.Key,
                    Cantidad = g.Count()
                })
                .ToList();

            // Lista de resultados procesados
            var resultados = new List<dynamic>();

            var contenidos = new List<Contenido>
            {
                votacion.Contenido,
                votacion.Contenido1,
                votacion.Contenido2,
                votacion.Contenido3
            };

            foreach (var contenido in contenidos.Where(c => c != null))
            {
                var votosConteo = votos.FirstOrDefault(v => v.ContenidoId == contenido.Id_Contenido);

                string imagenUrl = "/Content/Images/default.jpg";
                string nombreContenido = "Contenido no disponible";

                if (contenido.Pelicula != null)
                {
                    imagenUrl = contenido.Pelicula.ImgURL ?? "/Content/Images/default-movie.jpg";
                    nombreContenido = contenido.Pelicula.Nombre;
                }
                else if (contenido.Serie != null)
                {
                    imagenUrl = contenido.Serie.ImgURL ?? "/Content/Images/default-series.jpg";
                    nombreContenido = contenido.Serie.Nombre;
                }
                else if (contenido.Cancion != null)
                {
                    imagenUrl = contenido.Cancion.ImgURL ?? "/Content/Images/default-song.jpg";
                    nombreContenido = contenido.Cancion.Nombre;
                }

                resultados.Add(new
                {
                    ContenidoId = contenido.Id_Contenido,
                    Nombre = nombreContenido,
                    ImagenUrl = imagenUrl,
                    Votos = votosConteo?.Cantidad ?? 0
                });
            }

            // Calcular el total de votos
            var totalVotos = votos.Sum(v => v.Cantidad);

            ViewBag.Resultados = resultados;
            ViewBag.TotalVotos = totalVotos;

            return View(votacion);
        }

        // GET: Votacion/GetContenidosByTipo
        [HttpGet]
        public JsonResult GetContenidosByTipo(long tipoId)
        {
            var contenidos = db.Contenidoes
                .Where(c =>
                    (tipoId == 4 && c.Pelicula != null) ||
                    (tipoId == 3 && c.Serie != null) ||
                    (tipoId == 2 && c.Cancion != null))
                .Select(c => new
                {
                    Id = c.Id_Contenido,
                    Nombre = c.Pelicula != null ? c.Pelicula.Nombre :
                            c.Serie != null ? c.Serie.Nombre :
                            c.Cancion != null ? c.Cancion.Nombre : ""
                })
                .ToList();

            return Json(contenidos, JsonRequestBehavior.AllowGet);
        }

        // GET: Votacion/YaVoto
        [HttpGet]
        public JsonResult YaVoto(long votacionId)
        {
            if (Session["UserId"] == null)
            {
                return Json(new { yaVoto = false }, JsonRequestBehavior.AllowGet);
            }

            var userId = long.Parse(Session["UserId"].ToString());
            var yaVoto = db.Votoes.Any(v => v.Id_VotacionContenido == votacionId && v.Id_Usuario == userId);

            return Json(new { yaVoto = yaVoto }, JsonRequestBehavior.AllowGet);
        }

        private bool ContenidoPerteneceAVotacion(VotacionContenido votacion, long contenidoId)
        {
            return contenidoId == votacion.Id_Contenido1 ||
                   contenidoId == votacion.Id_Contenido2 ||
                   contenidoId == votacion.Id_Contenido3 ||
                   contenidoId == votacion.Id_Contenido4;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}