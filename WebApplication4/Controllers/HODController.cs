using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Data.Entity;

using WebApplication4.Models;

namespace WebApplication4.Controllers
{
    public class HODController : Controller
    {

        private readonly dummyclubsEntities _db = new dummyclubsEntities();
        private readonly EmailService _emailService = new EmailService();  // Injecting EmailService

        // GET: HOD
        public ActionResult Index()
        {
            // Check if session has required data
            if (Session["DepartmentID"] == null || Session["UniversityID"] == null)
            {
                return RedirectToAction("Login", "Admin");
            }

            int departmentId = Convert.ToInt32(Session["DepartmentID"]);
            int universityId = Convert.ToInt32(Session["UniversityID"]);
            string departmentName = Session["DepartmentName"]?.ToString();
            string universityName = Session["UniversityName"]?.ToString();

            // Count the number of clubs under this department
            int clubCount = _db.CLUBS.Count(c => c.DepartmentID == departmentId);

            // Pass values to view
            ViewBag.DepartmentName = departmentName;
            ViewBag.UniversityName = universityName;
            ViewBag.TotalClubs = clubCount;

            return View();
        }



        // ✅ Add Mentor - GET method
        [HttpGet]
        // GET: HOD/AddMentor
        public ActionResult AddMentor()
        {
            var model = new USER();
            model.DepartmentID = GetDepartmentID(); // Automatically set department
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> AddMentor(USER model, HttpPostedFileBase Photo)
        {
            if (ModelState.IsValid)
            {
                model.DepartmentID = GetDepartmentID(); // Auto-assign department
                model.UserType = "Mentor";
            }

                try
                {
                if (Photo != null && Photo.ContentLength > 0)
                {
                    string uploadDir = Server.MapPath("~/Uploads/");
                    if (!Directory.Exists(uploadDir)) Directory.CreateDirectory(uploadDir);

                    string fileName = Path.GetFileName(Photo.FileName);
                    string path = Path.Combine(uploadDir, fileName);
                    Photo.SaveAs(path);
                    model.PhotoPath = "~/Uploads/" + fileName;
                }

                model.RegistrationDate = DateTime.Now;
                model.IsActiveDate = DateTime.Now;
                model.IsActive = true;
                model.Userrole = "Mentor";
                model.DepartmentID = GetDepartmentID();

                _db.USERs.Add(model);
                await _db.SaveChangesAsync();

                var login = new Models.Login
                {
                    Email = model.Email,
                    PasswordHash = "Mentor@123", // For testing only
                    Role = "Mentor",
                    CreatedDate = DateTime.Now,
                    IsActive = true,
                    DepartmentID = GetDepartmentID()
                };

                _db.Logins.Add(login);
                await _db.SaveChangesAsync();

                string subject = "Welcome to Our Platform!";
                string body = $"Hello {model.FirstName},<br/><br/>" +
                              $"You have been successfully added as a mentor. Your login credentials are:<br/>" +
                              $"<strong>Email:</strong> {model.Email}<br/>" +
                              $"<strong>Password:</strong> Mentor@123<br/><br/>" +
                              "Please change your password after login.";

                await _emailService.SendEmailAsync(model.Email, subject, body);

                TempData["SuccessMessage"] = "Mentor added successfully.";
                return RedirectToAction("AddMentor", "HOD");
            }
            catch (Exception ex)
            {
                ViewBag.ErrorMessage = "Error: " + ex.Message;
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
            return Session["UserRole"] != null &&
                   Session["DepartmentID"] != null &&
                   ((string)Session["UserRole"]).Equals("HOD", StringComparison.OrdinalIgnoreCase);
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
            return RedirectToAction("Index", "UniversityAdmin"); // ✅ Redirecting to Mentor Dashboard
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
            // Step 1: Get logged-in HOD's email
            string email = User.Identity.Name;

            // Step 2: Get HOD's department ID from Logins table
            var hod = _db.Logins.FirstOrDefault(l => l.Email == email && l.Role == "HOD");
            if (hod == null)
                return HttpNotFound("HOD not found");

            int departmentId = (int)hod.DepartmentID;

            // Step 3: Get all club IDs under this department
            var clubIds = _db.CLUBS
                .Where(c => c.DepartmentID == departmentId)
                .Select(c => c.ClubID)
                .ToList();

            // Step 4: Get all events for those clubs that are pending HOD approval
            var events = _db.EVENTS
                .Where(e => e.ApprovalStatusID == 4 && clubIds.Contains((int)e.ClubID))
                .ToList();

            return View(events);
        }


    }
}