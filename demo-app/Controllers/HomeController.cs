using System.Web.Mvc;

namespace CSPGuardianDemo.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            return View();
        }

        public ActionResult About()
        {
            ViewBag.Message = "Your application description page.";
            return View();
        }

        public ActionResult Contact()
        {
            ViewBag.Message = "Your contact page.";
            return View();
        }

        [HttpPost]
        public JsonResult GetData()
        {
            // Simulate data retrieval
            var data = new[]
            {
                new { id = 1, name = "Item 1", email = "item1@example.com" },
                new { id = 2, name = "Item 2", email = "item2@example.com" },
                new { id = 3, name = "Item 3", email = "item3@example.com" }
            };
            return Json(data, JsonRequestBehavior.AllowGet);
        }
    }
}

