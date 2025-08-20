using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web.Mvc;
using System.Web.UI.WebControls;
using Microsoft.Win32;
using PagedList;
using SendGrid.Helpers.Mail;
using AppLogin = WebApplication4.Models.Login;
using WebApplication4.Models;

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

        public ActionResult RegisterClub()
        {
            if (!IsMentorLoggedIn())
            {
                return RedirectToAction("Login", "Admin");
            }

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

            // Pass to View
            ViewBag.DepartmentName = department.DepartmentName;
            ViewBag.DepartmentID = department.DepartmentID;

            // Optional: Pass subdepartment to view
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
            {
                return RedirectToAction("Login", "Admin");
            }

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
                // Set club properties
                club.MentorID = mentorID;
                club.CreatedDate = DateTime.Now;
                club.IsActive = false;

                club.DepartmentID = mentor.DepartmentID.GetValueOrDefault();
                club.SubDepartmentID = mentor.SubDepartmentID; // ✅ Store if mentor has one

                // Handle logo upload
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

                // Set status to PENDING
                club.ApprovalStatusID = _db.ApprovalStatusTables
                    .FirstOrDefault(a => a.Status == "PENDING")?.ApprovalStatusID ?? 1;

                _db.CLUBS.Add(club);
                _db.SaveChanges();

                // 🔐 Create login for club
                var clubEmail = club.ClubName.Replace(" ", "").ToLower() + "@yourdomain.com";

                var clubLogin = new AppLogin
                {
                    Email = clubEmail,
                    PasswordHash = "clubadmin@123",
                    Role = "Club Admin",
                    DepartmentID = club.DepartmentID,
                    SubDepartmentID = club.SubDepartmentID,
                    UniversityID = mentor.UniversityID,
                    ClubID = club.ClubID, // ✅ Add ClubID here
                    CreatedDate = DateTime.Now,
                    IsActive = true
                };



                // Optional: Set reference to ClubID if your LOGIN table has it
                // clubLogin.ClubID = club.ClubID; // Only if needed and available

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

            return View(); // No model is passed initially
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

            // Set the FilterApplied flag to true
            ViewBag.FilterApplied = true;

            // Fetch Organizer Names and Club Names separately and store them in ViewBag
            var loginIds = events.Select(e => e.EventOrganizerID).Distinct().ToList();

            var loginIdToEmail = _db.Logins
                .Where(l => loginIds.Contains(l.LoginID))
                .ToDictionary(l => l.LoginID, l => l.Email);

            var emailToName = _db.ClubRegistrations
                .Where(cr => loginIdToEmail.Values.Contains(cr.Email))
                .ToDictionary(cr => cr.Email, cr => cr.FullName);

            // Final dictionary: LoginID => FullName
            ViewBag.OrganizerNames = loginIdToEmail
                .Where(le => emailToName.ContainsKey(le.Value))
                .ToDictionary(le => le.Key.ToString(), le => emailToName[le.Value]);

            ViewBag.ClubNames = _db.CLUBS
                .ToDictionary(c => c.ClubID, c => c.ClubName);

            ViewBag.UniversityName = ViewBag.University; // Use ViewBag for university name

            ViewBag.Universities = _db.UNIVERSITies.ToList(); // Setting universities list to ViewBag

            return View(events); // Pass events list as the model
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ForwardEventToHOD(int eventId, int clubId)
        {
            if (Session["UserEmail"] == null)
            {
                return RedirectToAction("Login", "Admin");
            }

            string mentorEmail = Session["UserEmail"].ToString();
            var mentor = _db.Logins.FirstOrDefault(m => m.Email == mentorEmail);
            if (mentor == null)
            {
                return HttpNotFound("Mentor login not found.");
            }

            var eventToForward = _db.EVENTS.Find(eventId);
            if (eventToForward == null)
            {
                return HttpNotFound("Event not found");
            }

            var club = _db.CLUBS.FirstOrDefault(c => c.ClubID == clubId);
            if (club == null)
            {
                TempData["ErrorMessage"] = "Club not found.";
                return RedirectToAction("ViewEventRequests");
            }

            WebApplication4.Models.Login recipientLogin = null;
            string recipientRole = "";

            // First check if club belongs to a sub-department
            var subDept = _db.SUBDEPARTMENTs.FirstOrDefault(s => s.SubDepartmentID == club.SubDepartmentID);
            if (subDept != null && !string.IsNullOrEmpty(subDept.HOD_Email))
            {
                recipientLogin = _db.Logins.FirstOrDefault(l => l.Email == subDept.HOD_Email);
                recipientRole = "SubHOD";
            }

            // If SubHOD not found or not assigned, fallback to Department HOD
            if (recipientLogin == null)
            {
                var dept = _db.DEPARTMENTs.FirstOrDefault(d => d.DepartmentID == club.DepartmentID);
                if (dept == null || string.IsNullOrEmpty(dept.HOD_Email))
                {
                    TempData["ErrorMessage"] = "No HOD or SubHOD found for the club.";
                    return RedirectToAction("ViewEventRequests");
                }

                recipientLogin = _db.Logins.FirstOrDefault(l => l.Email == dept.HOD_Email);
                recipientRole = "HOD";
            }

            if (recipientLogin == null)
            {
                TempData["ErrorMessage"] = "Recipient login not found.";
                return RedirectToAction("ViewEventRequests");
            }

            // Mark event as pending HOD/SubHOD approval
            eventToForward.ApprovalStatusID = 4;

            try
            {
                _db.SaveChanges();

                // Notify event organizer
                if (eventToForward.EventOrganizerID != null)
                {
                    var organizerNotification = new Notification
                    {
                        LoginID = eventToForward.EventOrganizerID,
                        Message = $"✅ Your event '{eventToForward.EventName}' has been forwarded to the {recipientRole} for approval.",
                        IsRead = false,
                        StartDate = DateTime.Now,
                        EndDate = DateTime.Now.AddDays(7),
                        CreatedDate = DateTime.Now
                    };
                    _db.Notifications.Add(organizerNotification);
                }

                // Notify the recipient (HOD or SubHOD)
                var recipientNotification = new Notification
                {
                    LoginID = recipientLogin.LoginID,
                    Message = $"📬 A new event '{eventToForward.EventName}' from club '{club.ClubName}' requires your approval.",
                    IsRead = false,
                    StartDate = DateTime.Now,
                    EndDate = DateTime.Now.AddDays(7),
                    CreatedDate = DateTime.Now
                };
                _db.Notifications.Add(recipientNotification);

                _db.SaveChanges();
                TempData["SuccessMessage"] = $"Event forwarded to {recipientRole} successfully!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "An error occurred while forwarding the event: " + ex.Message;
            }

            return RedirectToAction("ViewEventRequests");
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RejectEventRequest(int eventId, string rejectionReason)
        {
            try
            {
                if (Session["UserEmail"] == null)
                {
                    if (Request.IsAjaxRequest())
                        return Json(new { success = false, message = "Session expired. Please login again." });

                    return RedirectToAction("Login", "Admin");
                }

                var ev = _db.EVENTS.FirstOrDefault(e => e.EventID == eventId);

                if (ev == null)
                {
                    if (Request.IsAjaxRequest())
                        return Json(new { success = false, message = "Event not found!" });

                    TempData["ErrorMessage"] = "Event not found!";
                    return RedirectToAction("ViewEventRequests");
                }

                ev.ApprovalStatusID = 3; // Rejected

                if (string.IsNullOrWhiteSpace(rejectionReason))
                    rejectionReason = "No specific reason provided.";

                ev.RejectionReason = rejectionReason; // ✅ Add this line to save the reason

                _db.SaveChanges();


                if (ev.EventOrganizerID == null)
                {
                    if (Request.IsAjaxRequest())
                        return Json(new { success = false, message = "Club Admin (Organizer) ID not found. Notification not sent." });

                    TempData["ErrorMessage"] = "Club Admin (Organizer) ID not found. Notification not sent.";
                    return RedirectToAction("ViewEventRequests");
                }

                var notification = new Notification
                {
                    LoginID = ev.EventOrganizerID,
                    Message = $"❌ Your event '{ev.EventName}' was rejected.\nReason: {rejectionReason}",
                    IsRead = false,
                    StartDate = DateTime.Now,
                    EndDate = DateTime.Now.AddDays(7),
                    CreatedDate = DateTime.Now
                };

                _db.Notifications.Add(notification);
                _db.SaveChanges();

                if (Request.IsAjaxRequest())
                {
                    return Json(new { success = true });
                }

                TempData["SuccessMessage"] = $"Event '{ev.EventName}' rejected successfully! Notification sent to club admin.";
                return RedirectToAction("ViewEventRequests");
            }
            catch (Exception ex)
            {
                // Return error to AJAX for debugging
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