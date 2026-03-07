using Microsoft.AspNetCore.Mvc;

namespace demo_app_core.Controllers;

public class HomeController : Controller
{
    public IActionResult Index()
    {
        return View();
    }

    public IActionResult About()
    {
        return View();
    }

    public IActionResult Contact()
    {
        return View();
    }

    [HttpPost]
    public JsonResult GetData()
    {
        var data = new[]
        {
            new { id = 1, name = "Item 1", email = "item1@example.com" },
            new { id = 2, name = "Item 2", email = "item2@example.com" },
            new { id = 3, name = "Item 3", email = "item3@example.com" }
        };
        return Json(data);
    }
}

