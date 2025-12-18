using System.Collections.Generic;
using System.Web.Mvc;

namespace CSPGuardianDemo.Controllers
{
    public class ProductsController : Controller
    {
        public ActionResult Index()
        {
            var products = new List<object>
            {
                new { Id = 1, Name = "Product 1", Price = 29.99m, Stock = 100 },
                new { Id = 2, Name = "Product 2", Price = 49.99m, Stock = 50 },
                new { Id = 3, Name = "Product 3", Price = 19.99m, Stock = 200 }
            };
            return View(products);
        }

        public ActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(FormCollection collection)
        {
            // Simulate product creation
            return RedirectToAction("Index");
        }

        public ActionResult Edit(int id)
        {
            ViewBag.ProductId = id;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult UpdateProduct(int id, FormCollection collection)
        {
            // Simulate product update
            return Json(new { success = true, message = "Product updated successfully" });
        }

        [HttpPost]
        public JsonResult DeleteProduct(int id)
        {
            // Simulate product deletion
            return Json(new { success = true, message = "Product deleted successfully" });
        }
    }
}

