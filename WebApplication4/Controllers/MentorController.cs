using Microsoft.Win32;
using PagedList;
using SendGrid.Helpers.Mail;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Web.UI.WebControls;
using WebApplication4.Models;
using AppLogin = WebApplication4.Models.Login;

namespace WebApplication4.Controllers
{
    public class MentorController : Controller
    {
        private readonly dummyclubsEntities _db = new dummyclubsEntities();
        private readonly EmailService _emailService = new EmailService();  // Injecting EmailService

        public ActionResult Index()
        {
            if (!IsMentorLoggedIn())
                return RedirectToAction("Login", "Admin");

            int mentorID = GetMentorID();

            // Get Mentor Login
            var mentor = _db.Logins.FirstOrDefault(m => m.LoginID == mentorID && m.Role == "Mentor");
            if (mentor == null)
            {
                TempData["ErrorMessage"] = "Mentor not found!";
                return RedirectToAction("Login", "Admin");
            }

            // Get USER Profile
            var user = _db.USERs.FirstOrDefault(u => u.Email == mentor.Email);
            if (user == null)
            {
                TempData["ErrorMessage"] = "User profile not found!";
                return RedirectToAction("Login", "Admin");
            }

            // Fetch only University Name directly using UniversityID from Logins table (mentor)
            string universityName = _db.UNIVERSITies
                .Where(u => u.UniversityID == mentor.UniversityID)
                .Select(u => u.UniversityNAME)
                .FirstOrDefault();

            ViewBag.UniversityName = universityName;


            // SubDepartment flow?
            bool isSubDepartmentMentor = user.SubDepartmentID != null;

            string departmentName = null;
            string subDepartmentName = null;

            USER hodUser = null;
            USER subHodUser = null;
            USER directorUser = null;

            if (isSubDepartmentMentor)
            {
                // Sub-department info
                var subDept = _db.SUBDEPARTMENTs.FirstOrDefault(s => s.SubDepartmentID == user.SubDepartmentID);
                if (subDept != null)
                {
                    subDepartmentName = subDept.SubDepartmentName;

                    // Sub HOD
                    subHodUser = _db.USERs.FirstOrDefault(u => u.Email == subDept.HOD_Email);

                    // Department
                    var dept = _db.DEPARTMENTs.FirstOrDefault(d => d.DepartmentID == subDept.DepartmentID);
                    departmentName = dept?.DepartmentName;

                    // Director
                    if (!string.IsNullOrEmpty(dept?.DirectorEmail))
                        directorUser = _db.USERs.FirstOrDefault(u => u.Email == dept.DirectorEmail);
                }
            }
            else
            {
                // Normal HOD flow
                var dept = _db.DEPARTMENTs.FirstOrDefault(d => d.DepartmentID == user.DepartmentID);
                departmentName = dept?.DepartmentName;

                // HOD
                if (!string.IsNullOrEmpty(dept?.HOD_Email))
                    hodUser = _db.USERs.FirstOrDefault(u => u.Email == dept.HOD_Email);
            }

            // Notifications
            var notifications = _db.Notifications
                .Where(n => n.LoginID == mentorID &&
                            (n.IsRead == false || n.IsRead == null) &&
                            n.EndDate > DateTime.Now)
                .ToList();

            // Clubs + Events
            var clubs = _db.CLUBS
                           .Include(c => c.EVENTS)
                           .Where(c => c.MentorID == mentorID)
                           .ToList();

            int clubCount = clubs.Count;
            int eventCount = clubs.Sum(c => c.EVENTS?.Count() ?? 0);
            var clubNames = clubs.Select(c => c.ClubName).ToList();
            var eventCounts = clubs.Select(c => c.EVENTS?.Count() ?? 0).ToList();

            // ✅ Pass data to view
            ViewBag.Mentor = mentor;
/*            ViewBag.University = university;
*/            ViewBag.IsSubDepartmentMentor = isSubDepartmentMentor;

            ViewBag.DepartmentName = departmentName;
            ViewBag.SubDepartmentName = subDepartmentName;

            ViewBag.HOD = hodUser;
            ViewBag.SubHOD = subHodUser;
            ViewBag.Director = directorUser;

            ViewBag.Notifications = notifications;
            ViewBag.ClubsCount = clubCount;
            ViewBag.EventsCount = eventCount;
            ViewBag.ClubNames = clubNames;
            ViewBag.EventCounts = eventCounts;
            ViewBag.MentorFullName = $"{user.FirstName} {user.LastName}";

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult MarkNotificationAsRead(int notificationId)
        {
            var note = _db.Notifications.FirstOrDefault(n => n.NotificationID == notificationId);
            if (note != null)
            {
                note.IsRead = true;
                _db.SaveChanges();
                return Json(new { success = true });
            }
            return Json(new { success = false });
        }


        // ✅ Utility: Check if Mentor is Logged In
        private bool IsMentorLoggedIn()
        {
            return Session["UserRole"] != null &&
                   (string)Session["UserRole"] == "Mentor" &&
                   Session["UserID"] != null;
        }

        // ✅ Utility: Get Mentor ID from Session
        private int GetMentorID()
        {
            return Convert.ToInt32(Session["UserID"]);
        }

        // ✅ Utility: Get University ID from Session
        private int GetUniversityID()
        {
            return Convert.ToInt32(Session["UniversityID"]);
        }



        [HttpGet]
        public ActionResult RegisterClub()
        {
            if (!IsMentorLoggedIn())
                return RedirectToAction("Login", "Admin");

            int mentorID = GetMentorID();
            var mentor = _db.Logins.FirstOrDefault(m => m.LoginID == mentorID && m.Role == "Mentor");
            if (mentor == null)
            {
                TempData["ErrorMessage"] = "Mentor not found!";
                return RedirectToAction("Login", "Admin");
            }

            var department = _db.DEPARTMENTs.FirstOrDefault(d => d.DepartmentID == mentor.DepartmentID);
            if (department == null)
            {
                TempData["ErrorMessage"] = "Department not assigned!";
                return RedirectToAction("Login", "Admin");
            }

            ViewBag.DepartmentName = department.DepartmentName;
            ViewBag.DepartmentID = department.DepartmentID;
            ViewBag.SubDepartmentID = mentor.SubDepartmentID;

            return View(new CLUB
            {
                DepartmentID = department.DepartmentID,
                SubDepartmentID = mentor.SubDepartmentID,
            });
        }

        [HttpPost]
        public ActionResult RegisterClub(CLUB club)
        {
            if (!IsMentorLoggedIn())
                return RedirectToAction("Login", "Admin");

            int mentorID = GetMentorID();
            var mentor = _db.Logins.FirstOrDefault(m => m.LoginID == mentorID && m.Role == "Mentor");
            var department = _db.DEPARTMENTs.FirstOrDefault(d => d.DepartmentID == mentor.DepartmentID);

            if (!ModelState.IsValid)
            {
                ViewBag.ErrorMessage = "Invalid input. Please fill all required fields.";
                ViewBag.DepartmentName = department.DepartmentName;
                ViewBag.DepartmentID = department.DepartmentID;
                return View(club);
            }

            try
            {
                // ===== Set Club Properties =====
                club.MentorID = mentorID;
                club.CreatedDate = DateTime.Now;
                club.IsActive = false;
                club.DepartmentID = mentor.DepartmentID.GetValueOrDefault();
                club.SubDepartmentID = mentor.SubDepartmentID;

                // Store email in CLUBS table
                club.ClubHeadEmail = club.ClubHeadEmail.Trim();

                // Store Club Head Name & Mobile
                club.ClubHeadName = club.ClubHeadName.Trim();
                club.ClubHeadMobile = club.ClubHeadMobile.Trim();

                // ===== Handle Logo Upload =====
                if (Request.Files["LogoImage"] != null && Request.Files["LogoImage"].ContentLength > 0)
                {
                    var file = Request.Files["LogoImage"];
                    var fileName = Path.GetFileName(file.FileName);
                    var uploadFolder = Server.MapPath("~/Uploads");

                    if (!Directory.Exists(uploadFolder))
                        Directory.CreateDirectory(uploadFolder);

                    var filePath = Path.Combine(uploadFolder, fileName);
                    file.SaveAs(filePath);
                    club.LogoImagePath = "/Uploads/" + fileName;
                }

                // ===== Set Pending Status =====
                club.ApprovalStatusID = _db.ApprovalStatusTables
                    .FirstOrDefault(a => a.Status == "PENDING")?.ApprovalStatusID ?? 1;

                _db.CLUBS.Add(club);
                _db.SaveChanges();

                // ===== Create Login for Club Admin =====
                var clubLogin = new AppLogin
                {
                    Email = club.ClubHeadEmail, // ✅ use same email
                    PasswordHash = "clubadmin@123",
                    Role = "Club Admin",
                    DepartmentID = club.DepartmentID,
                    SubDepartmentID = club.SubDepartmentID,
                    UniversityID = mentor.UniversityID,
                    ClubID = club.ClubID,
                    CreatedDate = DateTime.Now,
                    IsActive = true
                };

                _db.Logins.Add(clubLogin);
                _db.SaveChanges();

                ModelState.Clear();
                ViewBag.SuccessMessage = "Club registration request sent to SCC!";
                ViewBag.DepartmentName = department.DepartmentName;
                ViewBag.DepartmentID = department.DepartmentID;

                return View(new CLUB
                {
                    DepartmentID = department.DepartmentID,
                    SubDepartmentID = mentor.SubDepartmentID,
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in RegisterClub: {ex.Message}");
                ViewBag.ErrorMessage = "An error occurred while registering the club.";
                ViewBag.DepartmentName = department.DepartmentName;
                ViewBag.DepartmentID = department.DepartmentID;
                return View(club);
            }
        }





        public ActionResult ViewClubStatus(int? page, string filter = "all")
        {
            int pageSize = 3;
            int pageNumber = (page ?? 1);

            int loggedInMentorID = (int)Session["UserID"];
            IQueryable<CLUB> clubsQuery = _db.CLUBS
                                             .Include(c => c.DEPARTMENT.UNIVERSITY)
                                             .Where(c => c.MentorID == loggedInMentorID);

            // Apply filter based on the selected filter
            if (filter == "pending")
            {
                clubsQuery = clubsQuery.Where(c => c.ApprovalStatusID == 1);
            }
            else if (filter == "approved")
            {
                clubsQuery = clubsQuery.Where(c => c.ApprovalStatusID == 2);
            }
            else if (filter == "rejected")
            {
                clubsQuery = clubsQuery.Where(c => c.ApprovalStatusID == 3);
            }

            // Apply pagination and order by ClubName
            var clubs = clubsQuery.OrderBy(c => c.ClubName).ToPagedList(pageNumber, pageSize);

            var notifications = _db.Notifications
                                   .Where(n => n.LoginID == loggedInMentorID)
                                   .ToList();

            // Pass the current filter value to the view
            ViewBag.Filter = filter;
            ViewBag.Notifications = notifications;

            return View(clubs);
        }







/*
        //[Authorize] // Ensures only logged-in users can access
        public ActionResult ViewClubRegistrations()
        {
            // Get the logged-in Mentor's UserID from session
            if (Session["UserID"] == null)
            {
                return RedirectToAction("AccessDenied", "Home");
            }

            int loggedInMentorID = (int)Session["UserID"]; // Corrected variable name

            // Get clubs where the logged-in mentor is assigned
            var clubs = _db.CLUBS.Where(c => c.MentorID == loggedInMentorID)
                                 .Select(c => new { c.ClubID, c.ClubName })
                                 .ToList();

            ViewBag.Clubs = new SelectList(clubs, "ClubID", "ClubName");

            return View();
        }

        // Fetch registrations dynamically based on the selected club
        public ActionResult GetClubRegistrations(int clubId)
        {
            var registrations = _db.ClubRegistrations
                       .Where(r => r.ClubID == clubId)
                       .ToList() // Materialize the query before using .ToString()
                       .Select(r => new
                       {
                           r.RegistrationID,
                           r.FullName,
                           r.Email,
                           r.ContactNumber,
                           r.PreferredRole,
                           r.RoleJustification,
                           r.ProfileImagePath,
                           r.AssignedRole,
                           RegisteredAt = r.RegisteredAt.HasValue
                               ? r.RegisteredAt.Value.ToString("yyyy-MM-dd HH:mm")
                               : null
                       })
                       .ToList(); // Convert to list after transformation

            return Json(registrations, JsonRequestBehavior.AllowGet);
        }
        [HttpPost]


        public async Task<ActionResult> AssignRole(int registrationId, string role)
        {
            var registration = await _db.ClubRegistrations.FindAsync(registrationId);
            if (registration == null)
            {
                return View("Error");
            }

            registration.AssignedRole = role;
            registration.ApprovalStatusID = 2;
            await _db.SaveChangesAsync();

            // If the role is "Club Admin", store credentials and send an email
            if (role == "Club Admin")
            {
                var existingUser = await _db.Logins.FirstOrDefaultAsync(l => l.Email == registration.Email);
                if (existingUser == null)
                {
                    // New login entry
                    var newUser = new Login
                    {
                        Email = registration.Email,
                        PasswordHash = "clubadmin@123", // Default password
                        CreatedDate = DateTime.Now,
                        IsActive = true,
                        UniversityID = ViewBag.UniversityID,  // Ensure these values are correct
                        DepartmentID = ViewBag.DepartmentID,
                        Role = "Club Admin"
                    };

                    _db.Logins.Add(newUser);
                    await _db.SaveChangesAsync();

                    // ✅ Send welcome email
                    string subject = "Club Admin Role Assigned";
                    string body = $"Hello {registration.FullName},<br/><br/>" +
                                  $"You have been assigned as a Club Admin. Here are your login details:<br/>" +
                                  $"<strong>Username:</strong> {registration.Email}<br/>" +
                                  $"<strong>Password:</strong> clubadmin@123 (Please change your password upon login).<br/><br/>" +
                                  "Please log in and update your profile.";

                    await _emailService.SendEmailAsync(registration.Email, subject, body);
                }
            }

            return Json(new { success = true });
        }



        [HttpPost]
        public ActionResult LeaveClub(int registrationId)
        {
            var registration = _db.ClubRegistrations.Find(registrationId);
            if (registration != null)
            {
                registration.AssignedRole = "Club Member"; // Set role to "Club Member"
                registration.ApprovalStatusID = 2;
                _db.SaveChanges();
                return Json(new { success = true });
            }
            return Json(new { success = false });
        }*/



        [HttpGet]
        public ActionResult ChangePassword()
        {
            if (Session["UserEmail"] == null)
            {
                TempData["ErrorMessage"] = "Your session has expired. Please login again.";
                return RedirectToAction("Login", "Admin"); // Redirects to login if session is missing
            }

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var userEmail = Session["UserEmail"]?.ToString();

            if (string.IsNullOrEmpty(userEmail))
            {
                TempData["ErrorMessage"] = "Your session has expired. Please login again.";
                return RedirectToAction("Login", "Admin");
            }

            var user = _db.Logins.FirstOrDefault(u => u.Email == userEmail);

            if (user == null)
            {
                ModelState.AddModelError("", "User not found.");
                return View(model);
            }

            // Check current password (assumes plain text for now)
            if (user.PasswordHash != model.CurrentPassword)
            {
                ModelState.AddModelError("", "Current password is incorrect.");
                return View(model);
            }

            // Update password
            user.PasswordHash = model.NewPassword;
            _db.SaveChanges();

            TempData["Success"] = "Password changed successfully!";
            return RedirectToAction("ChangePassword", "Mentor"); // ✅ Redirecting to Mentor Dashboard
        }




// GET: View Event Requests
public ActionResult ViewEventRequests()
{
    if (Session["UserEmail"] == null)
    {
        return RedirectToAction("Login", "Admin");
    }

    string userEmail = Session["UserEmail"].ToString();
    var mentorLogin = _db.Logins.FirstOrDefault(l => l.Email == userEmail);

    if (mentorLogin == null)
    {
        return HttpNotFound("Mentor login not found");
    }

    ViewBag.Clubs = _db.CLUBS
        .Where(c => c.MentorID == mentorLogin.LoginID && c.ApprovalStatusID == 2)
        .ToList();

    return View(); // No model initially
}

// POST: View Event Requests (Filtered)
[HttpPost]
public ActionResult ViewEventRequests(int selectedClubId)
{
    if (Session["UserEmail"] == null)
    {
        return RedirectToAction("Login", "Admin");
    }

    string userEmail = Session["UserEmail"].ToString();
    var mentorLogin = _db.Logins.FirstOrDefault(l => l.Email == userEmail);

    if (mentorLogin == null)
    {
        return HttpNotFound("Mentor login not found");
    }

    ViewBag.Clubs = _db.CLUBS
        .Where(c => c.MentorID == mentorLogin.LoginID)
        .ToList();

    var events = _db.EVENTS
        .Where(e => e.ClubID == selectedClubId && e.ApprovalStatusID == 1)
        .ToList();

    // Generate token for each event
    foreach (var ev in events)
    {
        ev.Token = SecureHelper.Encrypt($"{ev.EventID}|{ev.ClubID}");
    }

    // Flag to indicate filtering applied
    ViewBag.FilterApplied = true;

    // Map Organizer Names
    var loginIds = events.Select(e => e.EventOrganizerID).Distinct().ToList();
    var loginIdToEmail = _db.Logins
        .Where(l => loginIds.Contains(l.LoginID))
        .ToDictionary(l => l.LoginID, l => l.Email);

    var emailToName = _db.ClubRegistrations
        .Where(cr => loginIdToEmail.Values.Contains(cr.Email))
        .ToDictionary(cr => cr.Email, cr => cr.FullName);

    ViewBag.OrganizerNames = loginIdToEmail
        .Where(le => emailToName.ContainsKey(le.Value))
        .ToDictionary(le => le.Key.ToString(), le => emailToName[le.Value]);

    ViewBag.ClubNames = _db.CLUBS.ToDictionary(c => c.ClubID, c => c.ClubName);

    ViewBag.UniversityName = "ICFAI Foundation for Higher Education"; // Hardcoded or fetch dynamically
    ViewBag.Universities = _db.UNIVERSITies.ToList();

    return View(events);
}






        [HttpGet]
        [AllowAnonymous]
        public async Task<ActionResult> ForwardEventToHOD(string token, bool fromEmail = true)
        {
            if (string.IsNullOrEmpty(token))
                return HttpNotFound("Token missing");

            string plainData;
            try
            {
                plainData = SecureHelper.Decrypt(token);
            }
            catch
            {
                return HttpNotFound("Invalid token");
            }

            var parts = plainData.Split('|');
            if (parts.Length < 2) return HttpNotFound("Invalid token data");

            int eventId = Convert.ToInt32(parts[0]);
            int clubId = Convert.ToInt32(parts[1]);
            bool isFromEmail = parts.Length >= 3 && parts[2] == "email"; // detect email vs app

            var eventToForward = _db.EVENTS.Find(eventId);
            var club = _db.CLUBS.Find(clubId);

            if (eventToForward == null || club == null)
                return HttpNotFound("Event or club not found");

            // 🚫 Prevent duplicate actions
            if (eventToForward.ApprovalStatusID == 3)
                return Content($"Event '{eventToForward.EventName}' was already rejected. You cannot forward it now.");
            if (eventToForward.ApprovalStatusID == 4)
                return Content($"Event '{eventToForward.EventName}' has already been forwarded.");

            // 👉 Decide recipient (SubHOD first, else HOD)
            WebApplication4.Models.Login recipientLogin = null;
            string recipientRole = "";

            var subDept = _db.SUBDEPARTMENTs.FirstOrDefault(s => s.SubDepartmentID == club.SubDepartmentID);
            if (subDept != null && !string.IsNullOrEmpty(subDept.HOD_Email))
            {
                recipientLogin = _db.Logins.FirstOrDefault(l => l.Email == subDept.HOD_Email);
                recipientRole = "SubHOD";
            }

            if (recipientLogin == null)
            {
                var dept = _db.DEPARTMENTs.FirstOrDefault(d => d.DepartmentID == club.DepartmentID);
                if (dept != null && !string.IsNullOrEmpty(dept.HOD_Email))
                {
                    recipientLogin = _db.Logins.FirstOrDefault(l => l.Email == dept.HOD_Email);
                    recipientRole = "HOD";
                }
            }

            if (recipientLogin != null)
            {
                // ✅ Always set ApprovalStatusID = 4
                eventToForward.ApprovalStatusID = 4;
                _db.SaveChanges();

                // In-app notification
                _db.Notifications.Add(new Notification
                {
                    LoginID = recipientLogin.LoginID,
                    Message = $"📬 A new event '{eventToForward.EventName}' from club '{club.ClubName}' requires your approval.",
                    IsRead = false,
                    StartDate = DateTime.Now,
                    EndDate = DateTime.Now.AddDays(7),
                    CreatedDate = DateTime.Now
                });
                _db.SaveChanges();

                // 🔑 Generate a clean 2-part token for HOD/SubHOD
                string newToken = SecureHelper.Encrypt($"{eventToForward.EventID}|{club.ClubID}|email");

                var emailSvc = new WebApplication4.Models.EmailService();
                string emailBody = GenerateEventEmailBody(eventToForward, club, recipientRole, newToken, Request);
                await emailSvc.SendEmailAsync(
                    recipientLogin.Email,
                    $"New Event Approval Needed: {eventToForward.EventName}",
                    emailBody
                );

                return Content($"✅ Event '{eventToForward.EventName}' forwarded to {recipientRole} successfully!");
            }

            return Content("❌ No valid recipient (SubHOD or HOD) found for this event.");
        }


        // POST: in-app forward (keep if you also want email to be sent when forwarding from the app)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ForwardEventToHOD(string token)
        {
            if (Session["UserEmail"] == null)
                return RedirectToAction("Login", "Admin");

            string mentorEmail = Session["UserEmail"].ToString();
            var mentor = _db.Logins.FirstOrDefault(m => m.Email == mentorEmail);
            if (mentor == null) return HttpNotFound("Mentor login not found.");

            string plainData;
            try
            {
                plainData = SecureHelper.Decrypt(token);
            }
            catch
            {
                return HttpNotFound("Invalid token.");
            }

            var parts = plainData.Split('|');
            if (parts.Length != 2) return HttpNotFound("Invalid token data.");

            int eventId = Convert.ToInt32(parts[0]);
            int clubId = Convert.ToInt32(parts[1]);

            var eventToForward = _db.EVENTS.Find(eventId);
            if (eventToForward == null) return HttpNotFound("Event not found");

            var club = _db.CLUBS.Find(clubId);
            if (club == null)
            {
                TempData["ErrorMessage"] = "Club not found.";
                return RedirectToAction("ViewEventRequests");
            }

            WebApplication4.Models.Login recipientLogin = null;
            string recipientRole = "";

            // Sub-dept first
            var subDept = _db.SUBDEPARTMENTs.FirstOrDefault(s => s.SubDepartmentID == club.SubDepartmentID);
            if (subDept != null && !string.IsNullOrEmpty(subDept.HOD_Email))
            {
                recipientLogin = _db.Logins.FirstOrDefault(l => l.Email == subDept.HOD_Email);
                recipientRole = "SubHOD";
            }

            if (recipientLogin == null)
            {
                var dept = _db.DEPARTMENTs.FirstOrDefault(d => d.DepartmentID == club.DepartmentID);
                if (dept != null && !string.IsNullOrEmpty(dept.HOD_Email))
                {
                    recipientLogin = _db.Logins.FirstOrDefault(l => l.Email == dept.HOD_Email);
                    recipientRole = "HOD";
                }
            }

            if (recipientLogin == null)
            {
                TempData["ErrorMessage"] = "No HOD or SubHOD found.";
                return RedirectToAction("ViewEventRequests");
            }

            // ✅ Always set ApprovalStatusID = 4
            eventToForward.ApprovalStatusID = 4;
            _db.SaveChanges();

            // Notify organizer
            if (eventToForward.EventOrganizerID != null)
            {
                _db.Notifications.Add(new Notification
                {
                    LoginID = eventToForward.EventOrganizerID,
                    Message = $"✅ Your event '{eventToForward.EventName}' has been forwarded to the {recipientRole} for approval.",
                    IsRead = false,
                    StartDate = DateTime.Now,
                    EndDate = DateTime.Now.AddDays(7),
                    CreatedDate = DateTime.Now
                });
            }

            // Notify recipient
            _db.Notifications.Add(new Notification
            {
                LoginID = recipientLogin.LoginID,
                Message = $"📬 A new event '{eventToForward.EventName}' from club '{club.ClubName}' requires your approval.",
                IsRead = false,
                StartDate = DateTime.Now,
                EndDate = DateTime.Now.AddDays(7),
                CreatedDate = DateTime.Now
            });

            _db.SaveChanges();

            // 📧 Optional email
            string newToken = SecureHelper.Encrypt($"{eventToForward.EventID}|{club.ClubID}");
            var emailSvc = new WebApplication4.Models.EmailService();
            string emailBody = GenerateEventEmailBody(eventToForward, club, recipientRole, newToken, Request);
            await emailSvc.SendEmailAsync(
                recipientLogin.Email,
                $"New Event Approval Needed: {eventToForward.EventName}",
                emailBody
            );

            TempData["SuccessMessage"] = $"Event forwarded to {recipientRole} successfully!";
            return RedirectToAction("ViewEventRequests");
        }



        private string GenerateEventEmailBody(EVENT ev, CLUB club, string recipientRole, string token, HttpRequestBase request)
        {
            string scheme = request?.Url?.Scheme ?? "https";
            string baseUrl = $"{scheme}://{request?.Url?.Host}{(request?.Url?.IsDefaultPort == false ? ":" + request.Url.Port : "")}";

            // Build action links
            string primaryHref;
            string secondaryHref;
            string primaryText;
            string secondaryText;

            if (recipientRole == "HOD")
            {
                primaryHref = Url.Action("ApproveEvent", "HOD", new { token }, scheme);
                secondaryHref = Url.Action("RejectEvent", "HOD", new { token }, scheme);
                primaryText = "Approve";
                secondaryText = "Reject";
            }
            else
            {
                // SubHOD
                primaryHref = Url.Action("ForwardToDirector", "SubHOD", new { token }, scheme);
                secondaryHref = Url.Action("RejectEvent", "SubHOD", new { token }, scheme);
                primaryText = "Forward to Director";
                secondaryText = "Reject";
            }

            // Optional poster/budget links
            string posterUrl = string.IsNullOrWhiteSpace(ev.EventPoster)
                ? null
                : (ev.EventPoster.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                    ? ev.EventPoster
                    : baseUrl + ev.EventPoster);

            string budgetUrl = string.IsNullOrWhiteSpace(ev.BudgetDocumentPath)
                ? null
                : (ev.BudgetDocumentPath.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                    ? ev.BudgetDocumentPath
                    : baseUrl + ev.BudgetDocumentPath);

            // Login link (only used in note for HOD)
            string loginUrl = Url.Action("Login", "Admin", null, scheme);

            // Add special note only for HOD
            string noteBlock = "";
            if (recipientRole == "HOD")
            {
                noteBlock = $@"
        <p style='background:#f8f9fa;padding:10px;border:1px solid #ddd;border-radius:5px;'>
            <strong>ℹ️ Note:</strong> If you want to <b>reduce the budget</b> or 
            <b>upload the signed approval document</b>, please log in to your dashboard:<br/>
            <a href='{loginUrl}' 
               style='display:inline-block;margin-top:6px;padding:8px 12px;background:#007bff;
                      color:#fff;text-decoration:none;border-radius:4px;'>
               Go to Dashboard
            </a>
        </p>
        <hr/>";
            }

            return $@"
<div style='font-family:Arial,sans-serif;font-size:14px;color:#000;line-height:1.5;'>
  <h3 style='margin:0 0 8px 0;'>Event approval required</h3>
  <p><b>Club:</b> {HttpUtility.HtmlEncode(club.ClubName)}</p>
  <p><b>Event:</b> {HttpUtility.HtmlEncode(ev.EventName)}</p>
  <p><b>Description:</b> {HttpUtility.HtmlEncode(ev.EventDescription)}</p>
  <p><b>Dates:</b> {ev.EventStartDateAndTime} – {ev.EventEndDateAndTime}</p>
  <p><b>Venue:</b> {HttpUtility.HtmlEncode(ev.Venue)}</p>
  <p><b>Budget:</b> {ev.EventBudget}</p>
  {(budgetUrl != null ? $"<p><b>Budget Document:</b> <a href='{budgetUrl}'>View</a></p>" : "")}

  {noteBlock}

  <div style='margin-top:16px;'>
    <a href='{primaryHref}' style='padding:10px 14px;background:#1e7e34;color:#fff;text-decoration:none;border-radius:4px;margin-right:8px;display:inline-block;'>{primaryText}</a>
    <a href='{secondaryHref}' style='padding:10px 14px;background:#dc3545;color:#fff;text-decoration:none;border-radius:4px;display:inline-block;'>{secondaryText}</a>
  </div>
</div>";
        }






        // ===========================
        // GET: Reject Event Request
        // ===========================
        [HttpGet]
        [AllowAnonymous]
        public ActionResult RejectEventRequest(string token)
        {
            if (string.IsNullOrEmpty(token))
                return HttpNotFound("Token missing");

            string plainData;
            try
            {
                plainData = SecureHelper.Decrypt(token);
            }
            catch
            {
                return HttpNotFound("Invalid token");
            }

            var parts = plainData.Split('|');
            if (parts.Length < 2)
                return HttpNotFound("Invalid token data");

            int eventId = Convert.ToInt32(parts[0]);
            int clubId = Convert.ToInt32(parts[1]);
            bool fromEmail = parts.Length >= 3 && parts[2] == "email"; // detect email flow

            var ev = _db.EVENTS.Find(eventId);
            if (ev == null) return HttpNotFound("Event not found");

            // Prevent duplicate actions
            if (ev.ApprovalStatusID == 4)
                return Content($"Event '{ev.EventName}' has already been forwarded. You cannot reject it now.");
            if (ev.ApprovalStatusID == 3)
                return Content($"Event '{ev.EventName}' is already rejected.");

            // Send token & event to view
            ev.Token = token;
            ViewBag.FromEmail = fromEmail; // optional, for conditional UI in view
            return View("RejectEventRequest", ev);
        }

        // ===========================
        // POST: Reject Event Request
        // ===========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AllowAnonymous]
        public async Task<ActionResult> RejectEventRequest(string token, string rejectionReason)
        {
            try
            {
                if (string.IsNullOrEmpty(token))
                    return Json(new { success = false, message = "Token missing." });

                // Decrypt token
                string plainData;
                try
                {
                    plainData = SecureHelper.Decrypt(token);
                }
                catch
                {
                    return Json(new { success = false, message = "Invalid token." });
                }

                var parts = plainData.Split('|');
                if (parts.Length < 2)
                    return Json(new { success = false, message = "Invalid token data." });

                int eventId = Convert.ToInt32(parts[0]);
                bool fromEmail = parts.Length >= 3 && parts[2] == "email"; // detect email flow

                // Require session only if NOT email flow
                if (!fromEmail && (Session["UserEmail"] == null || string.IsNullOrEmpty(Session["UserEmail"].ToString())))
                {
                    return Json(new { success = false, message = "Session expired. Please login again." });
                }

                var ev = _db.EVENTS.FirstOrDefault(e => e.EventID == eventId);
                if (ev == null) return Json(new { success = false, message = "Event not found!" });

                // Prevent rejection if already forwarded or rejected
                if (ev.ApprovalStatusID == 4)
                    return Json(new { success = false, message = $"Event '{ev.EventName}' has already been forwarded. You cannot reject it now." });

                if (ev.ApprovalStatusID == 3)
                    return Json(new { success = false, message = $"Event '{ev.EventName}' is already rejected." });

                // Update event as rejected
                ev.ApprovalStatusID = 3;
                ev.RejectionReason = string.IsNullOrWhiteSpace(rejectionReason)
                    ? "No specific reason provided."
                    : rejectionReason;
                _db.SaveChanges();

                // In-app notification to organizer (if exists)
                if (ev.EventOrganizerID != null)
                {
                    _db.Notifications.Add(new Notification
                    {
                        LoginID = ev.EventOrganizerID,
                        Message = $"❌ Your event '{ev.EventName}' was rejected.\nReason: {ev.RejectionReason}",
                        IsRead = false,
                        StartDate = DateTime.Now,
                        EndDate = DateTime.Now.AddDays(7),
                        CreatedDate = DateTime.Now
                    });
                    _db.SaveChanges();
                }

                // Email to Club Admin
                var clubLogin = _db.Logins.FirstOrDefault(l => l.ClubID == ev.ClubID);
                if (clubLogin != null && !string.IsNullOrEmpty(clubLogin.Email))
                {
                    var emailService = new EmailService();
                    string subject = $"Event '{ev.EventName}' Rejected";
                    string body = $@"
<p>Your event <strong>{ev.EventName}</strong> has been 
   <span style='color:red;font-weight:bold;'>rejected</span>.</p>
<p><strong>Reason:</strong> {ev.RejectionReason}</p>
<br/>
<p>Regards,<br/>University Event Management System</p>";
                    await emailService.SendEmailAsync(clubLogin.Email, subject, body);
                }

                return Json(new { success = true, message = "Event rejected successfully and email sent to club admin." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }







        // GET: Mentor/AwaitingConfirmation
        public ActionResult AwaitingConfirmation()
        {
            if (Session["UserEmail"] == null)
            {
                return RedirectToAction("Login", "Admin");
            }

            string userEmail = Session["UserEmail"].ToString();
            var mentorLogin = _db.Logins.FirstOrDefault(l => l.Email == userEmail);
            if (mentorLogin == null)
            {
                return HttpNotFound("Mentor login not found");
            }

            int mentorId = mentorLogin.LoginID;

            var events = _db.EVENTS
                            .Include(e => e.CLUB)
                            .Where(e => e.ApprovalStatusID == 6 &&
                                        e.CLUB.MentorID == mentorId)
                            .ToList();

            return View(events);
        }



        // POST: Mentor/ConfirmBudget
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ConfirmBudget(int eventId, bool accept, string rejectReason)
        {
            // 1. Pull the event (including CLUB so we can reach the admin)
            var ev = _db.EVENTS
                        .Include(e => e.CLUB)          // need CLUB for admin lookup
                        .FirstOrDefault(e => e.EventID == eventId);

            if (ev == null)
                return HttpNotFound();

            // 2. Update approval state
            if (accept)
            {
                ev.ApprovalStatusID = 2;   // Approved
                TempData["Message"] = "Event approved with revised budget.";
            }
            else
            {
                ev.ApprovalStatusID = 3;   // Rejected
                ev.RejectionReason = string.IsNullOrWhiteSpace(rejectReason)
                                      ? "No specific reason provided."
                                      : rejectReason;
                TempData["Message"] = "Event rejected – club will be notified.";
            }

            // 3. Find the club admin (adjust field name to your schema)
            int? adminId = ev.EventOrganizerID;        // many apps store organiser here
/*                           ?? ev.CLUB.ClubAdminID;    // or maybe on the CLUB row
*/
            if (adminId != null)
            {
                string notifMsg = accept
                    ? $"✅ Your event \"{ev.EventName}\" has been approved by the mentor."
                    : $"❌ Your event \"{ev.EventName}\" was rejected by the mentor.\nReason: {ev.RejectionReason}";

                _db.Notifications.Add(new Notification
                {
                    LoginID = adminId.Value,
                    Message = notifMsg,
                    IsRead = false,
                    StartDate = DateTime.Now,
                    EndDate = DateTime.Now.AddDays(7),
                    CreatedDate = DateTime.Now
                });

               /* // 4. Optional: send an e-mail as well
                var admin = _db.Logins.FirstOrDefault(l => l.LoginID == adminId);
                if (admin != null && !string.IsNullOrWhiteSpace(admin.Email))
                {
                    EmailHelper.Send(
                        to: admin.Email,
                        subject: "Event Budget Decision",
                        body: notifMsg.Replace("\n", "<br/>")  // simple HTML email
                    );
                }*/
            }

            // 5. Commit everything
            _db.SaveChanges();

            return RedirectToAction("AwaitingConfirmation");
        }





        //forgetpassword
        [HttpGet]
        public ActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        public async Task<ActionResult> ForgotPassword(LoginViewModel model)
        {
            var user = _db.Logins.FirstOrDefault(l => l.Email == model.Username);
            if (user == null)
            {
                ViewBag.Message = "Email not found.";
                return View();
            }

            var otp = new Random().Next(100000, 999999).ToString();

            user.OTP = otp;
            user.OTPExpiry = DateTime.Now.AddMinutes(5);
            _db.SaveChanges();

            var emailService = new EmailService();
            await emailService.SendEmailAsync(user.Email, "Your OTP Code", $"Your OTP is: {otp}");

            return RedirectToAction("VerifyOTP", new { email = user.Email });
        }


        //otpverify
        [HttpGet]
        public ActionResult VerifyOTP(string email)
        {
            return View(new VerifyOTPViewModel { Email = email });
        }

        [HttpPost]
        public ActionResult VerifyOTP(VerifyOTPViewModel model)
        {
            var user = _db.Logins.FirstOrDefault(l => l.Email == model.Email);

            if (user == null || user.OTP != model.OTP || user.OTPExpiry < DateTime.Now)
            {
                ViewBag.Message = "Invalid or expired OTP.";
                return View(model);
            }

            user.PasswordHash = model.NewPassword;
            user.OTP = null;
            user.OTPExpiry = null;

            _db.SaveChanges();

            ViewBag.Message = "Password reset successful!";
            return RedirectToAction("Login");
        }

    }
}