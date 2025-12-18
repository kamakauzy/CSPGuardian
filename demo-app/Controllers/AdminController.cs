using System.Web.Mvc;

namespace CSPGuardianDemo.Controllers
{
    [Authorize]
    public class AdminController : Controller
    {
        public ActionResult Dashboard()
        {
            ViewBag.UserCount = 150;
            ViewBag.OrderCount = 1250;
            ViewBag.Revenue = 45000.00m;
            return View();
        }

        [HttpPost]
        public JsonResult GetStatistics()
        {
            var stats = new
            {
                users = new { total = 150, active = 120, new = 5 },
                orders = new { total = 1250, pending = 45, completed = 1205 },
                revenue = new { today = 1250.00m, week = 8750.00m, month = 45000.00m }
            };
            return Json(stats);
        }
    }
}

