using System.Collections.Generic;
using System.Web.Mvc;

namespace CSPGuardianDemo.Controllers
{
    public class OrdersController : Controller
    {
        public ActionResult Index()
        {
            var orders = new List<object>
            {
                new { Id = 1001, CustomerName = "John Doe", Total = 129.99m, Status = "Pending" },
                new { Id = 1002, CustomerName = "Jane Smith", Total = 249.99m, Status = "Shipped" },
                new { Id = 1003, CustomerName = "Bob Johnson", Total = 79.99m, Status = "Delivered" }
            };
            return View(orders);
        }

        public ActionResult Details(int id)
        {
            ViewBag.OrderId = id;
            return View();
        }

        [HttpPost]
        public JsonResult UpdateStatus(int id, string status)
        {
            // Simulate status update
            return Json(new { success = true, message = $"Order {id} status updated to {status}" });
        }
    }
}

