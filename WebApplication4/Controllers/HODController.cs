using iTextSharp.text;
using iTextSharp.text.pdf;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Windows.Documents;
using System.Xml.Linq;
using WebApplication4.Filters;
using WebApplication4.Models;

namespace WebApplication4.Controllers
{
    public class HODController : BaseController
    {
        private readonly dummyclubsEntities _db = new dummyclubsEntities();
        private readonly EmailService _emailService = new EmailService();  // Injecting EmailService

        // GET: HOD
        public ActionResult Index()
        {
            // ---------- 1) Guard ----------
            if (Session["DepartmentID"] == null || Session["UniversityID"] == null)
                return RedirectToAction("Login", "Admin");

            int departmentId = Convert.ToInt32(Session["DepartmentID"]);
            int universityId = Convert.ToInt32(Session["UniversityID"]);
            string departmentName = Session["DepartmentName"]?.ToString();
            string universityName = Session["UniversityName"]?.ToString();

            // ---------- 2) Get HOD Info ----------
            string hodEmail = Session["UserEmail"]?.ToString();
            string hodName = _db.DEPARTMENTs
                                .Where(d => d.DepartmentID == departmentId)
                                .Select(d => d.HOD) // or the column name where you store it
                                .FirstOrDefault() ?? "HOD";


            // ---------- 3) Clubs ----------
            var clubs = _db.CLUBS.Where(c => c.DepartmentID == departmentId).ToList();
            int totalClubs = clubs.Count;
            int approvedClubs = clubs.Count(c => c.ApprovalStatusID == 2);
            int pendingClubs = clubs.Count(c => c.ApprovalStatusID == 1);
            int rejectedClubs = clubs.Count(c => c.ApprovalStatusID == 3);

            // ---------- 4) Mentors ----------
            var mentors = _db.Logins.Where(m => m.DepartmentID == departmentId && m.Role == "Mentor").ToList();
            int totalMentors = mentors.Count;
            int activeMentors = mentors.Count(m => m.IsActive == true);
            int inactiveMentors = mentors.Count(m => m.IsActive == false);

            // ---------- 5) Events ----------
            var clubIds = clubs.Select(c => c.ClubID).ToList();
            var events = _db.EVENTS.Where(e => e.ClubID.HasValue && clubIds.Contains(e.ClubID.Value)).ToList();
            int totalEvents = events.Count;
            int approvedEvents = events.Count(e => e.ApprovalStatusID == 2);
            int pendingEvents = events.Count(e => e.ApprovalStatusID == 1);
            int rejectedEvents = events.Count(e => e.ApprovalStatusID == 3);

            // ---------- 6) ViewBag Passing ----------
            ViewBag.HodName = hodName;
            ViewBag.DepartmentName = departmentName;
            ViewBag.UniversityName = universityName;

            ViewBag.TotalClubs = totalClubs;
            ViewBag.ApprovedClubs = approvedClubs;
            ViewBag.PendingClubs = pendingClubs;
            ViewBag.RejectedClubs = rejectedClubs;

            ViewBag.TotalMentors = totalMentors;
            ViewBag.ActiveMentors = activeMentors;
            ViewBag.InactiveMentors = inactiveMentors;

            ViewBag.TotalEvents = totalEvents;
            ViewBag.ApprovedEvents = approvedEvents;
            ViewBag.PendingEvents = pendingEvents;
            ViewBag.RejectedEvents = rejectedEvents;

            return View();
        }

        // ✅ Add Mentor - GET
        [HttpGet]
        public ActionResult AddMentor()
        {
            var model = new USER
            {
                DepartmentID = GetDepartmentID() // Auto-assign department
            };
            return View(model);
        }

        // ✅ Add Mentor - POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> AddMentor(USER model, HttpPostedFileBase Photo)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Errors = ModelState.Values.SelectMany(v => v.Errors)
                                                  .Select(e => e.ErrorMessage)
                                                  .ToList();
                return View(model);
            }

            try
            {
                int deptId = GetDepartmentID();
                model.DepartmentID = deptId;
                model.Userrole = "Mentor";
                model.UserType = "Campus";
                model.RegistrationDate = DateTime.Now;
                model.IsActiveDate = DateTime.Now;
                model.IsActive = true;

                // Handle photo upload
                if (Photo != null && Photo.ContentLength > 0)
                {
                    string uploadDir = Server.MapPath("~/Uploads/");
                    if (!Directory.Exists(uploadDir))
                        Directory.CreateDirectory(uploadDir);

                    string fileName = Guid.NewGuid() + Path.GetExtension(Photo.FileName);
                    Photo.SaveAs(Path.Combine(uploadDir, fileName));
                    model.PhotoPath = "/Uploads/" + fileName;
                }

                // Check if email exists in Logins
                var existingLogin = _db.Logins.FirstOrDefault(l => l.Email == model.Email);
                if (existingLogin != null)
                {
                    // Update roles
                    var roles = existingLogin.Role.Split(',').Select(r => r.Trim()).ToList();
                    if (!roles.Contains("Mentor"))
                    {
                        roles.Add("Mentor");
                        existingLogin.Role = string.Join(",", roles);
                    }

                    // Update DepartmentID & UniversityID
                    existingLogin.DepartmentID = deptId;
                    var department = _db.DEPARTMENTs.FirstOrDefault(d => d.DepartmentID == deptId);
                    existingLogin.UniversityID = department?.Universityid ?? existingLogin.UniversityID;

                    // Save or update USER table
                    var existingMentor = _db.USERs.FirstOrDefault(u => u.Email == model.Email && u.Userrole.Contains("Mentor"));
                    if (existingMentor != null)
                    {
                        existingMentor.FirstName = model.FirstName;
                        existingMentor.LastName = model.LastName;
                        existingMentor.PhotoPath = model.PhotoPath;
                        existingMentor.UserType = model.UserType;
                        existingMentor.IsActive = true;
                    }
                    else
                    {
                        _db.USERs.Add(model);
                    }
                }
                else
                {
                    // Create new Login
                    var department = _db.DEPARTMENTs.FirstOrDefault(d => d.DepartmentID == deptId);
                    var login = new Login
                    {
                        Email = model.Email,
                        PasswordHash = "Mentor@123",
                        Role = "Mentor",
                        CreatedDate = DateTime.Now,
                        IsActive = true,
                        DepartmentID = deptId,
                        UniversityID = department?.Universityid ?? 0
                    };
                    _db.Logins.Add(login);

                    // Add USER record
                    _db.USERs.Add(model);
                }

                // Save changes
                await _db.SaveChangesAsync();

                // Optional: send welcome email
                try
                {
                    string subject = "Welcome as Mentor";
                    string body = $"Hello {model.FirstName},<br/>" +
                                  $"You are added as a mentor. Email: {model.Email}, Password: Mentor@123";
                    await _emailService.SendEmailAsync(model.Email, subject, body);
                }
                catch { /* optionally log email errors */ }

                TempData["SuccessMessage"] = "Mentor details saved successfully.";
                return RedirectToAction("AddMentor");
            }
            catch (System.Data.Entity.Validation.DbEntityValidationException dbEx)
            {
                // Capture EF validation errors
                var errorList = new List<string>();
                foreach (var eve in dbEx.EntityValidationErrors)
                {
                    foreach (var ve in eve.ValidationErrors)
                    {
                        errorList.Add($"Property: {ve.PropertyName}, Error: {ve.ErrorMessage}");
                    }
                }
                ViewBag.Errors = errorList;
                return View(model);
            }
            catch (Exception ex)
            {
                // Generic errors
                ViewBag.Errors = new List<string> { "Error: " + ex.Message };
                return View(model);
            }
        }




        private int GetDepartmentID()
        {
            if (Session["DepartmentID"] != null)
            {
                return Convert.ToInt32(Session["DepartmentID"]);
            }
            throw new Exception("Department ID not found in session. HOD must be logged in.");
        }


        // MANAGE MENTORS (ALL - ACTIVE + INACTIVE)
        public ActionResult ManageMentors()
        {
            if (!IsHODLoggedIn())
            {
                return RedirectToAction("Login", "Login");
            }

            int departmentID = GetDepartmentID();

            var mentors = _db.USERs
                .Where(u => u.Userrole == "Mentor" && u.DepartmentID == departmentID)
                .ToList();

            return View("ManageMentors", mentors);
        }

        // ACTIVATE MENTOR
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ActivateMentor(string email)
        {
            if (!IsHODLoggedIn())
            {
                return RedirectToAction("Login", "Login");
            }

            int departmentID = GetDepartmentID();

            var mentor = _db.USERs.FirstOrDefault(m => m.Email == email && m.DepartmentID == departmentID);
            var login = _db.Logins.FirstOrDefault(l => l.Email == email);

            if (mentor != null && login != null)
            {
                mentor.IsActive = true;
                mentor.IsActiveDate = DateTime.Now;
                login.IsActive = true;

                _db.SaveChanges();
                TempData["SuccessMessage"] = "Mentor activated successfully.";
            }
            else
            {
                TempData["ErrorMessage"] = "Mentor not found or does not belong to your department.";
            }

            return RedirectToAction("ManageMentors");
        }

        // DEACTIVATE MENTOR
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeactivateMentor(string email)
        {
            if (!IsHODLoggedIn())
            {
                return RedirectToAction("Login", "Login");
            }

            int departmentID = GetDepartmentID();

            var mentor = _db.USERs.FirstOrDefault(m => m.Email == email && m.DepartmentID == departmentID);
            var login = _db.Logins.FirstOrDefault(l => l.Email == email);

            if (mentor != null && login != null)
            {
                mentor.IsActive = false;
                mentor.IsActiveDate = DateTime.Now;
                login.IsActive = false;

                _db.SaveChanges();
                TempData["SuccessMessage"] = "Mentor deactivated successfully.";
            }
            else
            {
                TempData["ErrorMessage"] = "Mentor not found or does not belong to your department.";
            }

            return RedirectToAction("ManageMentors");
        }

        // VIEW ALL MENTORS (OF DEPARTMENT)
        public ActionResult ViewMentors()
        {
            if (!IsHODLoggedIn())
            {
                return RedirectToAction("Login", "Login");
            }

            int departmentID = GetDepartmentID();

            var mentors = _db.USERs
                .Where(u => u.Userrole == "Mentor" && u.DepartmentID == departmentID)
                .ToList();

            return View("ViewMentors", mentors);
        }

        // VIEW ACTIVE MENTORS
        public ActionResult ViewActiveMentors()
        {
            if (!IsHODLoggedIn())
            {
                return RedirectToAction("Login", "Login");
            }

            int departmentID = GetDepartmentID();

            var mentors = _db.USERs
                .Where(u => u.Userrole == "Mentor"
                    && u.DepartmentID == departmentID
                    && u.IsActive == true)
                .ToList();

            return View("ViewMentors", mentors);
        }

        // VIEW DEACTIVATED MENTORS
        public ActionResult ViewDeactivatedMentors()
        {
            if (!IsHODLoggedIn())
            {
                return RedirectToAction("Login", "Login");
            }

            int departmentID = GetDepartmentID();

            var mentors = _db.USERs
                .Where(u => u.Userrole == "Mentor"
                    && u.DepartmentID == departmentID
                    && u.IsActive == false)
                .ToList();

            return View("ViewMentors", mentors);
        }

        private bool IsHODLoggedIn()
        {
            return Session["Role"] != null &&
                   Session["DepartmentID"] != null &&
                   ((string)Session["Role"]).Equals("HOD", StringComparison.OrdinalIgnoreCase);
        }

        // Club Requests (only under HOD's department)
        public ActionResult ClubRequests()
        {
            if (!IsHODLoggedIn())
            {
                return RedirectToAction("Login", "Login");
            }

            int departmentID = GetDepartmentID();

            var clubs = _db.CLUBS
                .Include(c => c.Login)
                .Include(c => c.DEPARTMENT.UNIVERSITY)
                .Where(c => c.DepartmentID == departmentID) // ✅ Filter by HOD's department
                .ToList();

            // ✅ Fetch Mentor Names from USERs Table Using Email
            foreach (var club in clubs)
            {
                var mentorUser = _db.USERs.FirstOrDefault(u => u.Email == club.Login.Email);
                club.MentorName = mentorUser != null ? mentorUser.FirstName + " " + mentorUser.LastName : "Not Assigned";
            }

            return View(clubs);
        }

        public ActionResult ApproveClub(int id)
        {
            var club = _db.CLUBS.Find(id);
            if (club != null)
            {
                club.ApprovalStatusID = 2; // Approved
                club.IsActive = true;
                _db.SaveChanges();

                // ✅ Create a notification with Start & End Date
                var notification = new Notification
                {
                    LoginID = club.MentorID,  // Mentor's LoginID
                    Message = $"Your club '{club.ClubName}' has been approved!",
                    IsRead = false,  // Unread by default
                    StartDate = DateTime.Now,  // Starts now
                    EndDate = DateTime.Now.AddDays(7),  // Expires in 7 days
                    CreatedDate = DateTime.Now
                };

                _db.Notifications.Add(notification);
                _db.SaveChanges();
            }

            return RedirectToAction("ClubRequests");
        }

        // ❌ Reject Club with Reason
        [HttpPost]
        public ActionResult RejectClub(int id, string reason)
        {
            var club = _db.CLUBS.Find(id);

            if (club != null)
            {
                // ❌ Update Status to 'Rejected'
                club.ApprovalStatusID = 3; // 3 = Rejected
                _db.SaveChanges();

                // ✅ Ensure reason is not empty
                if (string.IsNullOrWhiteSpace(reason))
                {
                    reason = "No specific reason provided."; // Default message
                }

                // ✅ Debug: Check if Mentor ID exists
                if (club.MentorID == null)
                {
                    System.Diagnostics.Debug.WriteLine("🚨 Error: MentorID is NULL for club ID: " + id);
                    TempData["ErrorMessage"] = "Mentor ID not found for this club!";
                    return RedirectToAction("ClubRequests");
                }

                // ✅ Create a notification for the mentor
                var notification = new Notification
                {
                    LoginID = club.MentorID, // Mentor's LoginID
                    Message = $"❌ Your club '{club.ClubName}' was rejected.\nReason: {reason}",
                    IsRead = false, // Mark as unread
                    StartDate = DateTime.Now,
                    EndDate = DateTime.Now.AddDays(7), // Notification expires in 7 days
                    CreatedDate = DateTime.Now
                };

                _db.Notifications.Add(notification);
                int changes = _db.SaveChanges(); // Save notification

                // ✅ Debug: Check if notification was added successfully
                if (changes > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"✅ Notification added successfully for MentorID: {club.MentorID} with reason: {reason}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("🚨 Error: Notification NOT added!");
                }

                TempData["SuccessMessage"] = $"Club '{club.ClubName}' rejected successfully!";
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("🚨 Error: Club not found for ID: " + id);
                TempData["ErrorMessage"] = "Club not found!";
            }

            return RedirectToAction("ClubRequests");
        }

        // ManageClubs action with pagination
        public ActionResult ManageClubs(int page = 1)
        {
            if (!IsHODLoggedIn())
            {
                return RedirectToAction("Login", "Login");
            }

            int departmentID = GetDepartmentID();  // Get HOD's department ID
            int pageSize = 5;

            // Filter clubs only for the HOD's department
            var clubs = _db.CLUBS
                .Where(c => c.DepartmentID == departmentID)
                .OrderBy(c => c.ClubName)
                .ToList();

            var pagedClubs = clubs.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            ViewBag.TotalItemCount = clubs.Count;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling((double)clubs.Count / pageSize);
            ViewBag.FirstItemOnPage = ((page - 1) * pageSize) + 1;
            ViewBag.LastItemOnPage = Math.Min(page * pageSize, clubs.Count);

            return View(pagedClubs);
        }

        public ActionResult ActivateClub(int id, int page = 1)
        {
            if (!IsHODLoggedIn())
            {
                return RedirectToAction("Login", "Login");
            }

            int departmentID = GetDepartmentID();
            var club = _db.CLUBS.FirstOrDefault(c => c.ClubID == id && c.DepartmentID == departmentID);

            if (club != null)
            {
                club.IsActive = true;
                _db.SaveChanges();
            }

            return RedirectToAction("ManageClubs", new { page });
        }

        public ActionResult DeactivateClub(int id, int page = 1)
        {
            if (!IsHODLoggedIn())
            {
                return RedirectToAction("Login", "Login");
            }

            int departmentID = GetDepartmentID();
            var club = _db.CLUBS.FirstOrDefault(c => c.ClubID == id && c.DepartmentID == departmentID);

            if (club != null)
            {
                club.IsActive = false;
                _db.SaveChanges();
            }

            return RedirectToAction("ManageClubs", new { page });
        }

        public ActionResult ClubStatus(int page = 1, int? status = null)
        {
            int pageSize = 5;

            // ✅ Get logged-in HOD's email from session
            string userEmail = Session["UserEmail"]?.ToString();
            if (string.IsNullOrEmpty(userEmail))
            {
                return RedirectToAction("Login", "Admin");
            }

            // ✅ Find the department of the logged-in HOD
            var hodLogin = _db.Logins.FirstOrDefault(l => l.Email == userEmail);
            if (hodLogin == null)
            {
                return HttpNotFound("HOD not found.");
            }

            int departmentID = hodLogin.DepartmentID ?? 0;

            // ✅ Build the query to fetch clubs under that department
            var clubsQuery = _db.CLUBS
                .Include(c => c.DEPARTMENT)
                .Include(c => c.DEPARTMENT.UNIVERSITY)
                .Include(c => c.ApprovalStatusTable)
                .Include(c => c.Login)
                .Where(c => c.DepartmentID == departmentID) // Filter by department
                .AsQueryable();

            // ✅ Filter by status if provided
            if (status.HasValue)
            {
                clubsQuery = clubsQuery.Where(c => c.ApprovalStatusID == status.Value);
            }

            // ✅ Pagination
            var totalClubs = clubsQuery.Count();
            var clubs = clubsQuery
                .OrderBy(c => c.ClubName)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            // ✅ Map Mentor Email -> First Name
            var mentorEmails = clubs
                .Where(c => c.Login != null)
                .Select(c => c.Login.Email)
                .Distinct()
                .ToList();

            var mentorNames = _db.USERs
                .Where(u => mentorEmails.Contains(u.Email))
                .ToDictionary(u => u.Email, u => u.FirstName);

            foreach (var club in clubs)
            {
                club.MentorName = (club.Login != null && mentorNames.ContainsKey(club.Login.Email))
                    ? mentorNames[club.Login.Email]
                    : "Unknown Mentor";
            }

            // ✅ Get rejection notifications for current mentors
            var mentorIds = clubs.Select(c => c.MentorID).ToList();
            var notifications = _db.Notifications
                .Where(n => n.LoginID.HasValue && mentorIds.Contains(n.LoginID.Value) && n.Message.Contains("rejected"))
                .ToList();

            // ✅ ViewBag for pagination and filters
            ViewBag.Notifications = notifications;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalClubs / pageSize);
            ViewBag.FirstItemOnPage = ((page - 1) * pageSize) + 1;
            ViewBag.LastItemOnPage = Math.Min(page * pageSize, totalClubs);
            ViewBag.TotalItemCount = totalClubs;
            ViewBag.CurrentStatus = status;

            return View(clubs);
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
            return RedirectToAction("ChangePassword", "HOD"); // ✅ Redirecting to Mentor Dashboard
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




        public ActionResult EventsForApproval()
        {
            int departmentId = Convert.ToInt32(Session["DepartmentID"]);

            // Step 3: Get all club IDs under this department
            var clubIds = _db.CLUBS
                .Where(c => c.DepartmentID == departmentId)
                .Select(c => c.ClubID)
                .ToList();

            // Step 4: Get all events for those clubs that are pending HOD approval
            var events = _db.EVENTS
                .Include(e => e.CLUB) // ✅ include navigation property
                .Where(e => e.ApprovalStatusID == 4 && clubIds.Contains((int)e.ClubID))
                .ToList();

            return View(events);
        }

        public ActionResult ViewEventDocument(int id)
        {
            var ev = _db.EVENTS.FirstOrDefault(e => e.EventID == id);
            if (ev == null || string.IsNullOrEmpty(ev.BudgetDocumentPath))
                return HttpNotFound();

            ViewBag.DocumentPath = ev.BudgetDocumentPath;
            ViewBag.EventName = ev.EventName;

            return View();
        }








        // ===========================
        // GET: Reject Event (HOD email link)
        // ===========================
        [HttpGet]
        [AllowAnonymous]
        public ActionResult RejectEvent(string token)
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
            if (parts.Length < 3) // expecting EventID | ClubID | RecipientLoginID
                return HttpNotFound("Invalid token data");

            int eventId = Convert.ToInt32(parts[0]);
            int recipientLoginId = Convert.ToInt32(parts[2]);

            // Ensure only intended recipient can access this
            if (Session["UserEmail"] != null)
            {
                var currentUser = _db.Logins.FirstOrDefault(l => l.Email == Session["UserEmail"].ToString());
                if (currentUser == null || currentUser.LoginID != recipientLoginId)
                    return Content("❌ You are not authorized to reject this event.");
            }

            var ev = _db.EVENTS.Include(e => e.CLUB).FirstOrDefault(e => e.EventID == eventId);
            if (ev == null)
                return HttpNotFound("Event not found");

            if (ev.ApprovalStatusID == 2)
                return Content($"Event '{ev.EventName}' has already been approved.");
            if (ev.ApprovalStatusID == 3)
                return Content($"Event '{ev.EventName}' has already been rejected.");

            ViewBag.Token = token; // pass token to form
            return View("RejectEvent", ev);
        }

        // ===========================
        // POST: Reject Event (HOD dashboard or email)
        // ===========================
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryTokenIfNoToken] // custom attribute for token-less dashboard
        public async Task<ActionResult> RejectEvent(int? eventId, string rejectionReason, string token = null)
        {
            try
            {
                int evtId;
                int? tokenRecipientId = null;

                if (!string.IsNullOrEmpty(token))
                {
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
                    if (parts.Length < 3)
                        return Json(new { success = false, message = "Invalid token data." });

                    evtId = Convert.ToInt32(parts[0]);
                    tokenRecipientId = Convert.ToInt32(parts[2]);

                    // Validate recipient
                    if (Session["UserEmail"] != null)
                    {
                        var currentUser = _db.Logins.FirstOrDefault(l => l.Email == Session["UserEmail"].ToString());
                        if (currentUser == null || currentUser.LoginID != tokenRecipientId)
                            return Json(new { success = false, message = "❌ You are not authorized to reject this event." });
                    }
                }
                else if (eventId.HasValue)
                {
                    evtId = eventId.Value;
                }
                else
                {
                    return Json(new { success = false, message = "Missing event information." });
                }

                var ev = _db.EVENTS.Include(e => e.CLUB).FirstOrDefault(e => e.EventID == evtId);
                if (ev == null || ev.CLUB == null)
                    return Json(new { success = false, message = "Event not found." });

                if (ev.ApprovalStatusID == 2)
                    return Json(new { success = false, message = $"Event '{ev.EventName}' has already been approved." });
                if (ev.ApprovalStatusID == 3)
                    return Json(new { success = false, message = $"Event '{ev.EventName}' has already been rejected." });

                // Update event
                ev.ApprovalStatusID = 3;
                ev.RejectionReason = string.IsNullOrWhiteSpace(rejectionReason) ? "No reason provided." : rejectionReason;

                string message = $"❌ Event '{ev.EventName}' from club '{ev.CLUB.ClubName}' was rejected by HOD.\nReason: {ev.RejectionReason}";
                var emailService = new EmailService();
                var emailTasks = new List<Task>();

                // --- Mentor ---
                if (ev.CLUB.MentorID != null)
                {
                    var mentorLogin = _db.Logins.FirstOrDefault(l => l.LoginID == ev.CLUB.MentorID);
                    if (mentorLogin != null)
                    {
                        _db.Notifications.Add(new Notification
                        {
                            LoginID = mentorLogin.LoginID,
                            Message = message,
                            IsRead = false,
                            StartDate = DateTime.Now,
                            EndDate = DateTime.Now.AddDays(7),
                            CreatedDate = DateTime.Now
                        });

                        if (!string.IsNullOrEmpty(mentorLogin.Email))
                            emailTasks.Add(emailService.SendEmailAsync(mentorLogin.Email, $"Event Rejected: {ev.EventName}", message));
                    }
                }

                // --- Club Admin ---
                var clubAdminLogin = _db.Logins.FirstOrDefault(l => l.ClubID == ev.ClubID);
                if (clubAdminLogin != null)
                {
                    _db.Notifications.Add(new Notification
                    {
                        LoginID = clubAdminLogin.LoginID,
                        Message = message,
                        IsRead = false,
                        StartDate = DateTime.Now,
                        EndDate = DateTime.Now.AddDays(7),
                        CreatedDate = DateTime.Now
                    });

                    if (!string.IsNullOrEmpty(clubAdminLogin.Email))
                        emailTasks.Add(emailService.SendEmailAsync(clubAdminLogin.Email, $"Event Rejected: {ev.EventName}", message));
                }

                _db.SaveChanges();

                if (emailTasks.Any())
                    await Task.WhenAll(emailTasks);

                if (!string.IsNullOrEmpty(token))
                    return Json(new { success = true, message = "Event rejected successfully and notifications sent!" });
                else
                {
                    TempData["Message"] = "Event rejected successfully.";
                    return RedirectToAction("EventsForApproval");
                }
            }
            catch (Exception ex)
            {
                if (!string.IsNullOrEmpty(token))
                    return Json(new { success = false, message = ex.Message });

                TempData["Error"] = ex.Message;
                return RedirectToAction("EventsForApproval");
            }
        }





        // ===========================
        // GET: Approve Event (HOD email link)
        // ===========================
        [HttpGet]
        [AllowAnonymous]
        public async Task<ActionResult> ApproveEvent(string token)
        {
            if (string.IsNullOrEmpty(token))
                return Content("❌ Invalid link.");

            string plainData;
            try
            {
                plainData = SecureHelper.Decrypt(token);
            }
            catch
            {
                return Content("❌ Invalid or expired token.");
            }

            var parts = plainData.Split('|');
            if (parts.Length < 3) // expecting EventID | ClubID | RecipientLoginID
                return Content("❌ Invalid token data.");

            int evtId = Convert.ToInt32(parts[0]);
            int recipientLoginId = Convert.ToInt32(parts[2]);

            // Ensure only intended recipient can approve
            if (Session["UserEmail"] != null)
            {
                var currentUser = _db.Logins.FirstOrDefault(l => l.Email == Session["UserEmail"].ToString());
                if (currentUser == null || currentUser.LoginID != recipientLoginId)
                    return Content("❌ You are not authorized to approve this event.");
            }

            var ev = _db.EVENTS.Include(e => e.CLUB).FirstOrDefault(e => e.EventID == evtId);
            if (ev == null || ev.CLUB == null)
                return Content("❌ Event not found.");

            // Prevent duplicate approval
            if (ev.ApprovalStatusID == 2)
                return Content("✅ Event already approved.");
            if (ev.ApprovalStatusID == 3)
                return Content("❌ Event already rejected.");

            // Full approval via email link
            decimal proposedBudget = 0;
            decimal.TryParse(ev.EventBudget, out proposedBudget);
            ev.ApprovedAmount = proposedBudget.ToString();
            ev.ApprovalStatusID = 2; // fully approved

            _db.SaveChanges();

            // Send notifications + emails to mentor and club admin
            //await SendHODApprovalNotificationsAndEmails(ev, false, proposedBudget);

            return Content($"✅ Event '{ev.EventName}' ({ev.CLUB.ClubName}) fully approved by HOD.");
        }

        // ===========================
        // POST: Approve Event (HOD dashboard or email)
        // ===========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ApproveEvent(int eventId,
                                          decimal? approvedAmount,
                                          HttpPostedFileBase signedDocument,
                                          string token = null)
        {
            var ev = _db.EVENTS.Include(e => e.CLUB)
                               .FirstOrDefault(e => e.EventID == eventId);
            if (ev == null) return HttpNotFound("Event not found.");

            // --- Ensure only token recipient can approve via email
            if (!string.IsNullOrEmpty(token))
            {
                string plainData;
                try
                {
                    plainData = SecureHelper.Decrypt(token);
                }
                catch
                {
                    return Content("❌ Invalid token.");
                }

                var parts = plainData.Split('|');
                if (parts.Length < 3)
                    return Content("❌ Invalid token data.");

                int recipientLoginId = Convert.ToInt32(parts[2]);

                if (Session["UserEmail"] != null)
                {
                    var currentUser = _db.Logins.FirstOrDefault(l => l.Email == Session["UserEmail"].ToString());
                    if (currentUser == null || currentUser.LoginID != recipientLoginId)
                        return Content("❌ You are not authorized to approve this event.");
                }
            }

            // --- Validate upload --------------------------------------------------
            if (signedDocument == null || signedDocument.ContentLength == 0)
            {
                TempData["Error"] = "Signed PDF is required.";
                return RedirectToAction("EventsForApproval", new { id = eventId });
            }
            if (!signedDocument.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = "Only PDF files are allowed.";
                return RedirectToAction("EventsForApproval", new { id = eventId });
            }

            // --- Save the PDF -----------------------------------------------------
            string uploadsRoot = Server.MapPath("~/uploads");
            Directory.CreateDirectory(uploadsRoot);

            string fileName = $"Signed_Approval_{eventId}_{DateTime.Now:yyyyMMddHHmmss}.pdf";
            string fullPath = Path.Combine(uploadsRoot, fileName);
            signedDocument.SaveAs(fullPath);

            ev.EventFormPath = "/uploads/" + fileName;

            // --- Determine approved amount ---------------------------------------
            decimal proposedBudget = 0;
            decimal.TryParse(ev.EventBudget, out proposedBudget);
            decimal finalBudget = (approvedAmount.HasValue && approvedAmount.Value > 0)
                                  ? approvedAmount.Value
                                  : proposedBudget;
            ev.ApprovedAmount = finalBudget.ToString();

            bool budgetReduced = finalBudget < proposedBudget;
            ev.ApprovalStatusID = budgetReduced ? 6 : 2;

            _db.SaveChanges();

            // --- Notifications ----------------------------------------------------
            string baseMsg = $"✅ Event '{ev.EventName}' ({ev.CLUB.ClubName}) ";
            var notifications = new List<Notification>();

            if (budgetReduced)
            {
                notifications.Add(new Notification
                {
                    LoginID = ev.CLUB.MentorID,
                    Message = baseMsg + $"was approved with a reduced budget of ₹{finalBudget:N0}. Please confirm.",
                    IsRead = false,
                    StartDate = DateTime.Now,
                    EndDate = DateTime.Now.AddDays(7),
                    CreatedDate = DateTime.Now
                });
            }
            else
            {
                notifications.Add(new Notification
                {
                    LoginID = ev.CLUB.MentorID,
                    Message = baseMsg + "has been fully approved.",
                    IsRead = false,
                    StartDate = DateTime.Now,
                    EndDate = DateTime.Now.AddDays(7),
                    CreatedDate = DateTime.Now
                });

                var clubReg = _db.ClubRegistrations.FirstOrDefault(c => c.ClubID == ev.ClubID);
                var clubAdminId = clubReg == null ? 0 :
                                  _db.Logins.Where(l => l.Email == clubReg.Email)
                                            .Select(l => l.LoginID)
                                            .FirstOrDefault();
                if (clubAdminId != 0)
                {
                    notifications.Add(new Notification
                    {
                        LoginID = clubAdminId,
                        Message = baseMsg + "is now fully approved.",
                        IsRead = false,
                        StartDate = DateTime.Now,
                        EndDate = DateTime.Now.AddDays(7),
                        CreatedDate = DateTime.Now
                    });
                }
            }

            _db.Notifications.AddRange(notifications);
            _db.SaveChanges();

            TempData["Message"] = "Event approval recorded successfully.";
            return RedirectToAction("EventsForApproval", new { id = eventId });
        }
    }
}