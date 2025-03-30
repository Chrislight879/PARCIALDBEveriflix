using PARCIALDBEveriflix.Models;
using System;
using System.Data.Entity;
using System.Linq;
using System.Web;
using System.Web.Mvc;

public class DisqueraController : Controller
{
    private EveryflixDBEntities db = new EveryflixDBEntities();

    [AuthorizeDisquera]
    public ActionResult DisqueraProfile() 
    {
        long userId = GetCurrentUserId();
        var disquera = db.Disqueras
            .Include(d => d.Artistas)
            .Include(d => d.Artistas.Select(a => a.Usuario))
            .FirstOrDefault(d => d.Id_Usuario == userId);

        if (disquera == null)
        {
            return HttpNotFound();
        }

        return View(disquera);
    }

    [AuthorizeDisquera]
    public ActionResult SearchArtists(string search)
    {
        // Get only artists without a disquera
        var artistas = db.Artistas
            .Include(a => a.Usuario)
            .Where(a => a.Id_Disquera == null) // Only artists without disquera
            .AsQueryable();

        if (!string.IsNullOrEmpty(search))
        {
            artistas = artistas.Where(a => a.Nombre.Contains(search));
        }

        return View(artistas.ToList());
    }

    [HttpPost]
    [AuthorizeDisquera]
    public ActionResult VincularArtista(long id)
    {
        long userId = GetCurrentUserId();
        var disquera = db.Disqueras.FirstOrDefault(d => d.Id_Usuario == userId);
        var artista = db.Artistas.Find(id);

        if (disquera == null || artista == null)
        {
            return HttpNotFound();
        }

        if (artista.Id_Disquera != null)
        {
            TempData["ErrorMessage"] = "El artista ya está vinculado a otra disquera";
            return RedirectToAction("SearchArtists");
        }

        artista.Id_Disquera = disquera.Id_Disquera;
        db.SaveChanges();

        TempData["SuccessMessage"] = $"Artista {artista.Nombre} vinculado correctamente";
        return RedirectToAction("DisqueraProfile");
    }

    [HttpPost]
    [AuthorizeDisquera]
    public ActionResult DesvincularArtista(long id)
    {
        long userId = GetCurrentUserId();
        var disquera = db.Disqueras.FirstOrDefault(d => d.Id_Usuario == userId);
        var artista = db.Artistas.Find(id);

        if (disquera == null || artista == null || artista.Id_Disquera != disquera.Id_Disquera)
        {
            return HttpNotFound();
        }

        artista.Id_Disquera = null;
        db.SaveChanges();

        TempData["SuccessMessage"] = $"Artista {artista.Nombre} desvinculado correctamente";
        return RedirectToAction("DisqueraProfile");
    }

    private long GetCurrentUserId()
    {
        if (Session["UserId"] == null) return 0;
        return Convert.ToInt64(Session["UserId"]);
    }
}

public class AuthorizeDisqueraAttribute : AuthorizeAttribute
{
    protected override bool AuthorizeCore(HttpContextBase httpContext)
    {
        if (httpContext.Session["UserType"] == null)
            return false;

        int userType = Convert.ToInt32(httpContext.Session["UserType"]);
        return userType == 4; // 4 = Disquera
    }

    protected override void HandleUnauthorizedRequest(AuthorizationContext filterContext)
    {
        filterContext.Result = new RedirectToRouteResult(
            new System.Web.Routing.RouteValueDictionary {
                { "controller", "Home" },
                { "action", "Index" },
                { "errorMessage", "Acceso restringido a disqueras" }
            });
    }
}