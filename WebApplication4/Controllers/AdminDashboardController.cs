using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Dynamic;



using System.Web.Mvc;

namespace WebApplication4.Controllers
{
    public class AdminDashboardController : Controller
    {
        // GET: Admin Dashboard
        public ActionResult Adminmethod()
        {
            if (Session["UserRole"]?.ToString() == "Admin")
            {
                ViewBag.UserName = Session["UserName"];
                return View();
            }
            return RedirectToAction("Login", "Admin");
        }




    }
}
