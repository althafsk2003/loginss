using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using WebApplication4.Models;
using Newtonsoft.Json;

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
            // Redirect to dashboard
            return RedirectToAction("Dashboard", "stulogin", new { enrollmentId = enrollmentNumber, email = email });

        }



        [HttpGet]
        public ActionResult Dashboard(string enrollmentId)
        {
            // Fetch email and event registrations
            var email = _db.EventRegistrations
                           .Where(r => r.EnrollmentId == enrollmentId)
                           .Select(r => r.Email)
                           .FirstOrDefault();

            var eventRegistrations = _db.EventRegistrations
                .Where(r => r.EnrollmentId == enrollmentId)
                .Select(r => new
                {
                    r.EVENT.EventName,
                    r.EVENT.EventDescription,  // Added EventDescription
                    r.EVENT.EventCreatedDate,  // Added EventDate
                    r.GroupName,
                    r.FullName,
                    r.Email,
                    r.RegisteredAt,
                    r.MemberNames
                })
                .ToList();

            // Prepare event data
            var events = eventRegistrations.Select(e => new EventRegistrationViewModel
            {
                EventName = e.EventName,
                EventDescription = e.EventDescription,  // Added EventDescription to model
                EventDate = e.EventCreatedDate,  // Added EventDate to model
                GroupName = e.GroupName,
                GroupLeader = !string.IsNullOrEmpty(e.GroupName)
                    ? _db.EventRegistrations
                          .Where(g => g.GroupName == e.GroupName)
                          .OrderBy(g => g.RegisteredAt)
                          .Select(g => g.FullName)
                          .FirstOrDefault()
                    : null,
                GroupMembers = !string.IsNullOrEmpty(e.GroupName)
                    ? _db.EventRegistrations
                          .Where(g => g.GroupName == e.GroupName)
                          .Select(g => new GroupMember
                          {
                              FullName = g.FullName,
                              Email = g.Email
                          })
                          .ToList()
                    : new List<GroupMember>()
            }).ToList();

            // Serialize the event registrations list for use in JavaScript
            var serializedEvents = JsonConvert.SerializeObject(events);

            // Prepare the student dashboard model
            var model = new StudentDashboardViewModel
            {
                EnrollmentId = enrollmentId,
               
                Email = email,
                RegisteredEvents = events,
                SerializedEvents = serializedEvents  // Add the serialized events to the model
            };

            return View(model);
        }


    }
}