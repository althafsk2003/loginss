using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using WebApplication4.Models;

namespace WebApplication4.Controllers
{
    public class stuloginController : Controller
    {
        // GET: stulogin
        private readonly dummyclubsEntities _db = new dummyclubsEntities();

        [HttpGet]
        public ActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public ActionResult Login(string enrollmentNumber, string email)
        {
            // Check if the enrollment number and email are registered
            var registration = _db.EventRegistrations
                                       .FirstOrDefault(r => r.EnrollmentId == enrollmentNumber && r.Email == email);
            if (registration == null)
            {
                ViewBag.Error = "No registration found with this enrollment number and email.";
                return View();
            }

            // Redirect to dashboard
            return RedirectToAction("Dashboard", "stulogin", new { enrollmentNumber = enrollmentNumber, email = email });
        }

        public ActionResult Dashboard(string enrollmentId)
        {
            // Fetch the events registered by the student using their Enrollment ID
            var events = _db.EventRegistrations
                                 .Where(r => r.EnrollmentId == enrollmentId)
                                 .Select(r => new
                                 {
                                     EventName = r.EVENT.EventName,
                                     RegisteredAt = r.RegisteredAt
                                 })
                                 .ToList();

            // Pass the student's details and event list to the view
            ViewBag.EnrollmentId = enrollmentId;
            ViewBag.Email = _db.EventRegistrations
                                    .Where(r => r.EnrollmentId == enrollmentId)
                                    .Select(r => r.Email)
                                    .FirstOrDefault();
            ViewBag.Events = events;

            return View();
        }


    }
}