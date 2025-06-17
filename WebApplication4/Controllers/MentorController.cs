using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Mvc;
using Microsoft.Win32;
using WebApplication4.Models;
using System.Data.Entity;
using System.Security.Claims;
using System.Threading.Tasks;
using PagedList;

namespace WebApplication4.Controllers
{
    public class MentorController : Controller
    {
        private readonly dummyclubsEntities _db = new dummyclubsEntities();
        private readonly EmailService _emailService = new EmailService();  // Injecting EmailService

        // ✅ Mentor Dashboard
        public ActionResult Index()
        {
            if (!IsMentorLoggedIn())
                return RedirectToAction("Login", "Admin");

            int mentorID = GetMentorID();
            System.Diagnostics.Debug.WriteLine($"[DEBUG] Mentor LoginID: {mentorID}");

            // 🔹 Fetch mentor info
            var mentor = _db.Logins
                            .FirstOrDefault(m => m.LoginID == mentorID && m.Role == "Mentor");
            if (mentor == null)
            {
                TempData["ErrorMessage"] = "Mentor not found!";
                return RedirectToAction("Login", "Admin");
            }

            var user = _db.USERs.FirstOrDefault(u => u.Email == mentor.Email);
            if (user == null)
            {
                TempData["ErrorMessage"] = "User profile not found!";
                return RedirectToAction("Login", "Admin");
            }

            // 🔹 University
            var university = _db.UNIVERSITies
                                .FirstOrDefault(u => u.UniversityID == mentor.UniversityID);

            // 🔹 Department
            var department = _db.DEPARTMENTs
                                .FirstOrDefault(d => d.DepartmentID == mentor.DepartmentID);

            // 🔹 Notifications
            var notifications = _db.Notifications
                .Where(n => n.LoginID == mentorID &&
                            (n.IsRead == false || n.IsRead == null) &&  // ✅ Fix nullable bool
                            n.EndDate > DateTime.Now)
                .ToList();


            // 🔹 Clubs + Events under mentor
            var clubs = _db.CLUBS
                           .Include(c => c.EVENTS)
                           .Where(c => c.MentorID == mentorID)
                           .ToList();

            int clubCount = clubs.Count;
            int eventCount = clubs.Sum(c => c.EVENTS?.Count() ?? 0);

            var clubNames = clubs.Select(c => c.ClubName).ToList();
            var eventCounts = clubs.Select(c => c.EVENTS?.Count() ?? 0).ToList();

            // ✅ Send to View
            ViewBag.Mentor = mentor;
            ViewBag.University = university;
            ViewBag.Department = department;
            ViewBag.Notifications = notifications;
            ViewBag.ClubsCount = clubCount;
            ViewBag.EventsCount = eventCount;
            ViewBag.ClubNames = clubNames;
            ViewBag.EventCounts = eventCounts;
            ViewBag.MentorFullName = $"{user.FirstName} {user.LastName}";

            return View();
        }


        // ✅ Club Registration (Mentors can register clubs under their university)
        //public ActionResult RegisterClub()
        //{
        //    if (!IsMentorLoggedIn())
        //    {
        //        return RedirectToAction("Login", "Admin");
        //    }

        //    return View(new CLUB()); // Create new club model
        //}

        //[HttpPost]
        //public ActionResult RegisterClub(CLUB club)
        //{
        //    if (!IsMentorLoggedIn())
        //    {
        //        return RedirectToAction("Login", "Admin");
        //    }

        //    if (!ModelState.IsValid)
        //    {
        //        ViewBag.ErrorMessage = "Invalid input. Please fill all required fields.";
        //        return View(club);
        //    }

        //    try
        //    {
        //        int universityID = GetUniversityID();
        //        int mentorID = GetMentorID();

        //        club.UniversityID = universityID;
        //        club.MentorID = mentorID;
        //        club.CreatedDate = DateTime.Now;
        //        club.IsActive = true;

        //        _db.CLUBs.Add(club);
        //        _db.SaveChanges();

        //        TempData["SuccessMessage"] = "Club registered successfully!";
        //        return RedirectToAction("ManageClubs");
        //    }
        //    catch (Exception ex)
        //    {
        //        System.Diagnostics.Debug.WriteLine($"Error in RegisterClub: {ex.Message}");
        //        ViewBag.ErrorMessage = "An error occurred while registering the club.";
        //        return View(club);
        //    }
        //}

        //// ✅ Manage Clubs (Mentors can view clubs they registered)
        //public ActionResult ManageClubs()
        //{
        //    if (!IsMentorLoggedIn())
        //    {
        //        return RedirectToAction("Login", "Admin");
        //    }

        //    int mentorID = GetMentorID();
        //    var clubs = _db.CLUBs.Where(c => c.MentorID == mentorID).ToList();

        //    return View(clubs);
        //}

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

            int mentorID = GetMentorID(); // Get Mentor ID

            // Fetch mentor's department
            var mentor = _db.Logins.FirstOrDefault(m => m.LoginID == mentorID && m.Role == "Mentor");

            if (mentor == null)
            {
                TempData["ErrorMessage"] = "Mentor not found!";
                return RedirectToAction("Login", "Admin");
            }

            // Fetch department details
            var department = _db.DEPARTMENTs.FirstOrDefault(d => d.DepartmentID == mentor.DepartmentID);

            if (department == null)
            {
                TempData["ErrorMessage"] = "Department not assigned!";
                return RedirectToAction("Login", "Admin");
            }

            // Pass department details to the view
            ViewBag.DepartmentName = department.DepartmentName;
            ViewBag.DepartmentID = department.DepartmentID;

            return View(new CLUB() { DepartmentID = department.DepartmentID }); // Pre-select the department
        }

        [HttpPost]
        public ActionResult RegisterClub(CLUB club)
        {
            if (!IsMentorLoggedIn())
            {
                return RedirectToAction("Login", "Admin");
            }

            int mentorID = GetMentorID(); // Get Mentor ID

            // Fetch mentor's department
            var mentor = _db.Logins.FirstOrDefault(m => m.LoginID == mentorID && m.Role == "Mentor");
            var department = _db.DEPARTMENTs.FirstOrDefault(d => d.DepartmentID == mentor.DepartmentID);

            if (!ModelState.IsValid)
            {
                // Re-fetch departments and return the view with an error message
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
                club.IsActive = false; // Initially inactive until admin approval

                // Handle logo file upload
                if (Request.Files["LogoImage"] != null && Request.Files["LogoImage"].ContentLength > 0)
                {
                    var file = Request.Files["LogoImage"];
                    var fileName = Path.GetFileName(file.FileName);

                    // Define the folder path where the file will be stored
                    string uploadFolder = Server.MapPath("~/Uploads");

                    // Create the directory if it does not exist
                    if (!Directory.Exists(uploadFolder))
                    {
                        Directory.CreateDirectory(uploadFolder);
                    }

                    // Define the file path inside the Uploads folder
                    var filePath = Path.Combine(uploadFolder, fileName);
                    file.SaveAs(filePath);

                    // Store relative file path in DB
                    club.LogoImagePath = "/Uploads/" + fileName;
                }

                // Set ApprovalStatusID to 'PENDING'
                club.ApprovalStatusID = _db.ApprovalStatusTables
                    .FirstOrDefault(a => a.Status == "PENDING")?.ApprovalStatusID ?? 1;

                // Add to the database
                _db.CLUBS.Add(club);
                _db.SaveChanges();

                // Clear ModelState to reset form fields
                ModelState.Clear();

                // Provide success message in the same view
                ViewBag.SuccessMessage = "Club registration request sent to HOD!";
                ViewBag.DepartmentName = department.DepartmentName;
                ViewBag.DepartmentID = department.DepartmentID;
                return View(new CLUB() { DepartmentID = department.DepartmentID });
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



        private int GetLoginID()
        {
            if (Session["UserID"] != null)
            {
                return Convert.ToInt32(Session["UserID"]); // Return the stored LoginID
            }
            return 0; // Return 0 if no user is logged in
        }





        public ActionResult MarkNotificationAsRead(int notificationId)
        {
            System.Diagnostics.Debug.WriteLine($"[DEBUG] MarkNotificationAsRead called with ID: {notificationId}");

            var notification = _db.Notifications.FirstOrDefault(n => n.NotificationID == notificationId);

            if (notification != null)
            {
                notification.IsRead = true;  // ✅ Mark as read
                _db.SaveChanges();

                System.Diagnostics.Debug.WriteLine($"[DEBUG] Notification {notificationId} marked as read.");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[DEBUG] Notification {notificationId} NOT FOUND!");
            }

            return RedirectToAction("Index"); // Refresh dashboard
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








        //[Authorize] // Ensures only logged-in users can access
        public ActionResult ViewClubRegistrations()
        {
            // Get the logged-in Mentor's UserID from session
            if (Session["UserID"] == null)
            {
                return RedirectToAction("AccessDenied", "Home");
            }

            int loggedInMentorID = (int)Session["UserID"];

            // Get clubs where the logged-in mentor is assigned
            var clubs = _db.CLUBS.Where(c => c.MentorID == loggedInMentorID)
                                 .Select(c => new { c.ClubID, c.ClubName })
                                 .ToList();

            ViewBag.Clubs = new SelectList(clubs, "ClubID", "ClubName");

            // Materialize club IDs into a list to use in Contains
            var clubIds = _db.CLUBS
                             .Where(c => c.MentorID == loggedInMentorID)
                             .Select(c => c.ClubID)
                             .ToList(); // Materialize to List<int>

            // Calculate total registration count for the mentor across all their clubs
            var totalRegistrations = _db.ClubRegistrations
                               .Where(r => r.ClubID.HasValue && clubIds.Contains(r.ClubID.Value))
                               .Count();

            ViewBag.TotalRegistrations = totalRegistrations;

            return View();
        }

        // Fetch registrations dynamically based on the selected club
        public ActionResult GetClubRegistrations(int clubId)
        {
            var registrations = _db.ClubRegistrations
                       .Where(r => r.ClubID == clubId)
                       .ToList()
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
                       .ToList();

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

            if (role == "Club Admin")
            {
                var existingUser = await _db.Logins.FirstOrDefaultAsync(l => l.Email == registration.Email);
                if (existingUser == null)
                {
                    var newUser = new Login
                    {
                        Email = registration.Email,
                        PasswordHash = "clubadmin@123",
                        CreatedDate = DateTime.Now,
                        IsActive = true,
                        UniversityID = ViewBag.UniversityID,
                        DepartmentID = ViewBag.DepartmentID,
                        Role = "Club Admin"
                    };

                    _db.Logins.Add(newUser);
                    await _db.SaveChangesAsync();

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
        }



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

            var userEmail = Session["Email"]?.ToString();

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
            return RedirectToAction("Index", "Mentor"); // ✅ Redirecting to Mentor Dashboard
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
                .Where(c => c.MentorID == mentorLogin.LoginID)
                .ToList();

            return View(); // No model is passed initially
        }

        // POST: View Event Requests (Filtered)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ViewEventRequests(int selectedClubId)
        {
            // 1. Basic guards
            if (Session["UserEmail"] == null)
                return Json(new { success = false, html = "Session expired" });

            string userEmail = Session["UserEmail"].ToString();
            var login = _db.Logins.FirstOrDefault(l => l.Email == userEmail);
            if (login == null)
                return Json(new { success = false, html = "User not found" });

            // 2. Pull pending events for the chosen club
            var events = _db.EVENTS
                           .Where(e => e.ClubID == selectedClubId && e.ApprovalStatusID == 1)
                           .ToList();

            // 3. Lookup dictionaries for friendly names
            var organizerNames = _db.Logins.ToDictionary(x => x.LoginID.ToString(), x => x.Email);
            var clubNames = _db.CLUBS.ToDictionary(x => x.ClubID, x => x.ClubName);

            // 4. Build the HTML string
            var sb = new System.Text.StringBuilder();

            if (!events.Any())
            {
                sb.Append("<div class='alert alert-info mt-4'>No events are requested for the selected club.</div>");
            }
            else
            {
                sb.Append("<div class='row row-cols-1 g-4'>");

                foreach (var item in events)
                {
                    string poster = !string.IsNullOrEmpty(item.EventPoster)
                        ? $"<div class='event-card-image'><img src='{item.EventPoster}' class='img-fluid rounded' /></div>"
                        : "";

                    string budget = !string.IsNullOrEmpty(item.BudgetDocumentPath)
                        ? $"<strong>Budget:</strong> <a href='{item.BudgetDocumentPath}' target='_blank'>View Budget</a><br />"
                        : "";

                    string organizer = organizerNames.TryGetValue(item.EventOrganizerID.ToString(), out var name)
                                       ? name
                                       : "Unknown";

                    string clubName = clubNames.TryGetValue((int)item.ClubID, out var cName)
                                       ? cName
                                       : "Unknown Club";

                    sb.Append($@"
<div class='col'>
  <div class='event-card'>
    <div class='event-card-header'>Event Details</div>

    <div class='event-content'>
      {poster}
      <div class='event-card-details'>
        <strong>Event Name:</strong> {item.EventName}<br />
        <strong>Description:</strong> {item.EventDescription}<br />
        <strong>Event Type:</strong> {item.EventType}<br />
        <strong>Start Date:</strong> {item.EventStartDateAndTime}<br />
        <strong>End Date:</strong> {item.EventEndDateAndTime}<br />
        <strong>Organizer:</strong> {organizer}<br />
        <strong>Club:</strong> {clubName}<br />
        <strong>University:</strong> ICFAI Foundation for Higher Education<br />
        {budget}
      </div>
    </div>

    <div class='event-card-buttons'>
      <!-- Post to HOD -->
      <form method='post' action='/Mentor/ForwardEventToHOD?eventId={item.EventID}&clubId={item.ClubID}'>
        <!-- placeholder token; JS will inject the real value -->
        <input name='__RequestVerificationToken' type='hidden' value='' />
        <button type='submit' class='btn btn-success'>Post to HOD</button>
      </form>

      <!-- Reject flow -->
      <div class='mt-2'>
        <button type='button' class='btn btn-danger' onclick='toggleRejectBox({item.EventID})'>Reject</button>
        <div id='reject-box-{item.EventID}' class='reject-box mt-2' style='display:none;'>
          <textarea id='rejection-text-{item.EventID}' class='form-control mb-2' placeholder='Enter rejection reason...' required></textarea>
          <button type='button' class='btn btn-sm btn-danger' onclick='submitRejection({item.EventID})'>Submit Rejection</button>
        </div>
      </div>
    </div>
  </div>
</div>");
                }

                sb.Append("</div>");
            }

            // 5. Return JSON with the generated markup
            return Json(new { success = true, html = sb.ToString() });
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ForwardEventToHOD(int eventId, int clubId)
        {
            if (Session["UserEmail"] == null)
            {
                return RedirectToAction("Login", "Admin");
            }

            var eventToForward = _db.EVENTS.Find(eventId);
            if (eventToForward == null)
            {
                return HttpNotFound("Event not found");
            }

            // Find the department associated with the club
            var club = _db.CLUBS.FirstOrDefault(c => c.ClubID == clubId);
            if (club == null)
            {
                TempData["ErrorMessage"] = "Club not found.";
                return RedirectToAction("ViewEventRequests");
            }

            // Find the department and HOD's email
            var department = _db.DEPARTMENTs.FirstOrDefault(d => d.DepartmentID == club.DepartmentID);
            if (department == null || string.IsNullOrEmpty(department.HOD_Email))
            {
                TempData["ErrorMessage"] = "Department or HOD email not found for the club.";
                return RedirectToAction("ViewEventRequests");
            }

            // Find the HOD's LoginID using HODEmail
            var hodLogin = _db.Logins.FirstOrDefault(l => l.Email == department.HOD_Email);
            if (hodLogin == null)
            {
                TempData["ErrorMessage"] = "HOD login not found for the email.";
                return RedirectToAction("ViewEventRequests");
            }

            // Mark the event as "Pending HOD Approval"
            eventToForward.ApprovalStatusID = 4; // 4 = Pending HOD Approval

            try
            {
                _db.SaveChanges();

                // Notify the event organizer
                if (eventToForward.EventOrganizerID != null)
                {
                    var organizerNotification = new Notification
                    {
                        LoginID = eventToForward.EventOrganizerID,
                        Message = $"✅ Your event '{eventToForward.EventName}' has been forwarded to the HOD for approval.",
                        IsRead = false,
                        StartDate = DateTime.Now,
                        EndDate = DateTime.Now.AddDays(7),
                        CreatedDate = DateTime.Now
                    };
                    _db.Notifications.Add(organizerNotification);
                }

                // Notify the HOD
                var hodNotification = new Notification
                {
                    LoginID = hodLogin.LoginID,
                    Message = $"📬 A new event '{eventToForward.EventName}' from club '{club.ClubName}' requires your approval.",
                    IsRead = false,
                    StartDate = DateTime.Now,
                    EndDate = DateTime.Now.AddDays(7),
                    CreatedDate = DateTime.Now
                };
                _db.Notifications.Add(hodNotification);

                _db.SaveChanges();

                TempData["SuccessMessage"] = "Event forwarded to HOD successfully!";
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
            _db.SaveChanges();

            if (string.IsNullOrWhiteSpace(rejectionReason))
                rejectionReason = "No specific reason provided.";

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
