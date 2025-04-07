using System.Linq;
using System.Data.Entity;
using System.Web.Mvc;
using PARCIALDBEveriflix.Models;

public class ContenidoController : Controller
{
    private EveryflixDBEntities db = new EveryflixDBEntities();

    // Acción para la vista general de búsqueda
    public ActionResult Buscar(string searchQuery)
    {
        // Configuración para evitar proxies
        db.Configuration.ProxyCreationEnabled = false;
        db.Configuration.LazyLoadingEnabled = false;

        // Obtener contenidos con sus relaciones
        var contenidos = db.Contenidoes
            .Include(c => c.Pelicula)
            .Include(c => c.Cancion)
            .Include(c => c.Cancion.Album) // Incluir álbum para canciones
            .Include(c => c.Cancion.Artista) // Incluir artista para canciones
            .Include(c => c.Serie)
            .Where(c => string.IsNullOrEmpty(searchQuery) ||
                       (c.Pelicula != null && c.Pelicula.Nombre.Contains(searchQuery)) ||
                       (c.Cancion != null && c.Cancion.Nombre.Contains(searchQuery)) ||
                       (c.Serie != null && c.Serie.Nombre.Contains(searchQuery)))
            .ToList();

        return View(contenidos);
    }
}
