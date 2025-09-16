using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace WebApplication4.Controllers
{
    public class BaseController : Controller
    {
        protected bool IsRole(string role) =>
            Session["CurrentRole"] != null &&
            Session["CurrentRole"].ToString().Equals(role, StringComparison.OrdinalIgnoreCase);

        protected void EnsureRole(string role)
        {
            if (!IsRole(role))
            {
                TempData["Message"] = "Access Denied.";
                RedirectToAction("Login", "Admin").ExecuteResult(ControllerContext);
            }
        }
    }

}