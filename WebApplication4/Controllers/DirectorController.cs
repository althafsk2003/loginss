using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using WebApplication4.Filters;
using WebApplication4.Models;

namespace WebApplication4.Controllers
{
    public class DirectorController : Controller
    {
        private readonly dummyclubsEntities _db = new dummyclubsEntities();
        private readonly EmailService _emailService = new EmailService();  // Injecting EmailService

        public ActionResult Index()
        {
            if (Session["UserRole"]?.ToString() != "Director")
            {
                return RedirectToAction("Login", "Admin");
            }

            int departmentId = Convert.ToInt32(Session["DepartmentID"]);
            var department = _db.DEPARTMENTs.FirstOrDefault(d => d.DepartmentID == departmentId);

            if (department == null)
            {
                ViewBag.ErrorMessage = "Department not found.";
                return View();
            }

            var university = _db.UNIVERSITies.FirstOrDefault(u => u.UniversityID == department.Universityid);

            ViewBag.DirectorName = department.DirectorName;
            ViewBag.DepartmentName = department.DepartmentName;
            ViewBag.UniversityName = university?.UniversityNAME ?? "Unknown University";

            // 1️⃣ Get all Sub-HODs for this department
            var subHods = _db.Logins
                .Where(l => l.DepartmentID == departmentId && l.Role.ToLower() == "subhod")
                .ToList();

            ViewBag.HodActiveCount = subHods.Count(s => s.IsActive == true);
            ViewBag.HodInactiveCount = subHods.Count(s => s.IsActive == false || s.IsActive == null);

            // 2️⃣ Get all Mentors in this department
            var mentors = _db.Logins
                .Where(l => l.DepartmentID == departmentId && l.Role.ToLower() == "mentor")
                .ToList();

            ViewBag.MentorActiveCount = mentors.Count(m => m.IsActive == true);
            ViewBag.MentorInactiveCount = mentors.Count(m => m.IsActive == false || m.IsActive == null);

            // 3️⃣ Get all Clubs in this department
            var clubs = _db.CLUBS
                .Where(c => c.DepartmentID == departmentId)
                .ToList();

            ViewBag.ClubActiveCount = clubs.Count(c => c.IsActive == true);
            ViewBag.ClubInactiveCount = clubs.Count(c => c.IsActive == false || c.IsActive == null);

            return View();
        }

        // add sub departments

        [HttpGet]
        public ActionResult AddSubDepartments()
        {
            if (Session["UserRole"]?.ToString() != "Director")
                return RedirectToAction("Login", "Admin");

            // Return empty model with one entry
            return View(new SubDepartmentPageViewModel
            {
                HODs = new List<SubDepartmentInputViewModel>()
            });
        }

        [HttpPost]
        public async Task<ActionResult> AddSubDepartments(SubDepartmentPageViewModel model)
        {
            if (Session["UserRole"]?.ToString() != "Director")
                return RedirectToAction("Login", "Admin");

            int departmentID = Convert.ToInt32(Session["DepartmentID"]);
            int universityID = Convert.ToInt32(Session["UniversityID"]);

            try
            {
                foreach (var hod in model.HODs)
                {
                    if (string.IsNullOrWhiteSpace(hod.HODName) || string.IsNullOrWhiteSpace(hod.HODEmail))
                        continue;

                    foreach (var subDeptName in hod.SubDepartments ?? new List<string>())
                    {
                        if (string.IsNullOrWhiteSpace(subDeptName)) continue;

                        var subDept = new SUBDEPARTMENT
                        {
                            SubDepartmentName = subDeptName,
                            DepartmentID = departmentID,
                            HOD = hod.HODName,
                            HOD_Email = hod.HODEmail,
                            CreatedDate = DateTime.Now,
                            IsActive = true,
                            IsActiveDate = DateTime.Now
                        };

                        _db.SUBDEPARTMENTs.Add(subDept);
                    }

                    if (!_db.Logins.Any(l => l.Email == hod.HODEmail && l.Role == "SUBHOD"))
                    {
                        _db.Logins.Add(new Login
                        {
                            Email = hod.HODEmail,
                            PasswordHash = "SubHod@123",
                            Role = "SUBHOD",
                            IsActive = true,
                            DepartmentID = departmentID,
                            UniversityID = universityID,
                            CreatedDate = DateTime.Now
                        });

                        await _emailService.SendEmailAsync(hod.HODEmail, "Welcome HOD!",
                            $"Hello {hod.HODName},<br/><br/>" +
                            $"You have been assigned as HOD for multiple sub-departments under your main department.<br/>" +
                            $"<strong>Username:</strong> {hod.HODEmail}<br/>" +
                            $"<strong>Password:</strong> SubHod@123<br/><br/>" +
                            $"Please login and change your password.");
                    }
                }

                await _db.SaveChangesAsync();
                TempData["SuccessMessage"] = "Sub-departments and HODs added successfully.";
                return RedirectToAction("AddSubDepartments");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error in AddSubDepartments: " + ex.Message);
                ViewBag.ErrorMessage = "An error occurred while adding sub-departments.";
                return View(model);
            }
        }

        // view sccs

        public ActionResult ViewSubHods(string filter = "manage")
        {
            // Check user role and redirect if not Director
            if (Session["UserRole"]?.ToString() != "Director")
            {
                return RedirectToAction("Login", "Admin");
            }

            // Get DepartmentID from session
            int departmentId = 0;
            if (!int.TryParse(Session["DepartmentID"]?.ToString(), out departmentId) || departmentId == 0)
            {
                ViewBag.ErrorMessage = "Department not found or invalid.";
                return View(new SubHodDashboardViewModel()); // Return empty model or error view
            }

            var department = _db.DEPARTMENTs.FirstOrDefault(d => d.DepartmentID == departmentId);
            if (department == null)
            {
                ViewBag.ErrorMessage = "Department not found.";
                return View(new SubHodDashboardViewModel()); // Return empty model or error view
            }

            // Query SubHODs for this department
            var subHods = (from sd in _db.SUBDEPARTMENTs
                           join l in _db.Logins on sd.HOD_Email equals l.Email
                           join dept in _db.DEPARTMENTs on sd.DepartmentID equals dept.DepartmentID
                           where sd.DepartmentID == departmentId
                           select new SubHodViewModel
                           {
                               Id = l.LoginID,
                               Name = sd.HOD, // Name from SUBDEPARTMENT table
                               Email = l.Email,
                               DepartmentName = dept.DepartmentName,
                               SubDepartmentName = sd.SubDepartmentName,
                               IsActive = l.IsActive ?? false
                           }).ToList();

            // Filter based on toggle buttons
            List<SubHodViewModel> filteredList;

            switch (filter.ToLower())
            {
                case "all":
                    filteredList = subHods;
                    break;

                case "active":
                    filteredList = subHods.Where(s => s.IsActive).ToList();
                    break;

                case "inactive":
                    filteredList = subHods.Where(s => !s.IsActive).ToList();
                    break;

                default: // "manage"
                    filteredList = subHods;
                    break;
            }


            var vm = new SubHodDashboardViewModel
            {
                TableData = filteredList,
                ShowToggle = filter.ToLower() == "manage",
                TotalCount = subHods.Count,
                ActiveCount = subHods.Count(s => s.IsActive),
                InactiveCount = subHods.Count(s => !s.IsActive)
            };

            return View(vm);
        }

        [HttpPost]
        public JsonResult ToggleSubHodStatus(int id, bool isActive)
        {
            if (Session["UserRole"]?.ToString() != "Director")
            {
                return Json(new { success = false, message = "Unauthorized" });
            }

            try
            {
                var subHod = _db.Logins.FirstOrDefault(l => l.LoginID == id && l.Role == "SubHOD");
                if (subHod == null)
                {
                    return Json(new { success = false, message = "Sub-HOD not found" });
                }

                subHod.IsActive = isActive;
                _db.SaveChanges();

                return Json(new { success = true, message = "Status updated successfully" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // view mentors 


        public ActionResult ViewMentors(string filter = "manage")
        {
            // Check user role (only Director can access)
            if (Session["UserRole"]?.ToString() != "Director")
            {
                return RedirectToAction("Login", "Admin");
            }

            // Get DepartmentID from session
            if (!int.TryParse(Session["DepartmentID"]?.ToString(), out int departmentId) || departmentId == 0)
            {
                ViewBag.ErrorMessage = "Department not found or invalid.";
                return View(new MentorDashboardViewModel());
            }

            var department = _db.DEPARTMENTs.FirstOrDefault(d => d.DepartmentID == departmentId);
            if (department == null)
            {
                ViewBag.ErrorMessage = "Department not found.";
                return View(new MentorDashboardViewModel());
            }

            // Get mentors for this department
            var mentors = (from l in _db.Logins
                           join u in _db.USERs on l.Email equals u.Email
                           join sd in _db.SUBDEPARTMENTs on l.SubDepartmentID equals sd.SubDepartmentID
                           join dept in _db.DEPARTMENTs on sd.DepartmentID equals dept.DepartmentID
                           where l.Role == "Mentor" && sd.DepartmentID == departmentId
                           select new MentorViewModel
                           {
                               LoginID = l.LoginID,
                               FullName = u.FirstName + " " + u.LastName, // Full Name from USERS table
                               Email = l.Email,
                               DepartmentName = dept.DepartmentName,
                               SubDepartmentName = sd.SubDepartmentName,
                               IsActive = l.IsActive ?? false
                           }).ToList();

            // Apply filters
            List<MentorViewModel> filteredList;
            switch (filter.ToLower())
            {
                case "all":
                    filteredList = mentors;
                    break;
                case "active":
                    filteredList = mentors.Where(m => m.IsActive).ToList();
                    break;
                case "inactive":
                    filteredList = mentors.Where(m => !m.IsActive).ToList();
                    break;
                default: // manage
                    filteredList = mentors;
                    break;
            }

            var vm = new MentorDashboardViewModel
            {
                TableData = filteredList,
                ShowToggle = filter.ToLower() == "manage",
                TotalCount = mentors.Count,
                ActiveCount = mentors.Count(m => m.IsActive),
                InactiveCount = mentors.Count(m => !m.IsActive)
            };

            return View(vm);
        }

        [HttpPost]
        public JsonResult ToggleMentorStatus(int id, bool isActive)
        {
            if (Session["UserRole"]?.ToString() != "Director")
            {
                return Json(new { success = false, message = "Unauthorized" });
            }

            try
            {
                var mentor = _db.Logins.FirstOrDefault(l => l.LoginID == id && l.Role == "Mentor");
                if (mentor == null)
                {
                    return Json(new { success = false, message = "Mentor not found" });
                }

                mentor.IsActive = isActive;
                _db.SaveChanges();

                return Json(new { success = true, message = "Status updated successfully" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }


        // clubs section
        // View Clubs
        public ActionResult ViewClubs(string filter = "manage")
        {
            // Check role
            if (Session["UserRole"]?.ToString() != "Director")
            {
                return RedirectToAction("Login", "Admin");
            }

            // Get DepartmentID from session
            if (!int.TryParse(Session["DepartmentID"]?.ToString(), out int departmentId) || departmentId == 0)
            {
                ViewBag.ErrorMessage = "Department not found or invalid.";
                return View(new ClubDashboardViewModel());
            }

            var department = _db.DEPARTMENTs.FirstOrDefault(d => d.DepartmentID == departmentId);
            if (department == null)
            {
                ViewBag.ErrorMessage = "Department not found.";
                return View(new ClubDashboardViewModel());
            }

            // Get clubs under this department
            var clubs = (from c in _db.CLUBS
                         join sd in _db.SUBDEPARTMENTs on c.SubDepartmentID equals sd.SubDepartmentID
                         join dept in _db.DEPARTMENTs on sd.DepartmentID equals dept.DepartmentID
                         where sd.DepartmentID == departmentId
                         select new ClubViewModel
                         {
                             ClubID = c.ClubID,
                             ClubName = c.ClubName,
                             DepartmentName = dept.DepartmentName,
                             SubDepartmentName = sd.SubDepartmentName,
                             IsActive = c.IsActive ?? false
                         }).ToList();

            // Apply filters
            List<ClubViewModel> filteredList;
            switch (filter.ToLower())
            {
                case "all":
                    filteredList = clubs;
                    break;
                case "active":
                    filteredList = clubs.Where(c => c.IsActive).ToList();
                    break;
                case "inactive":
                    filteredList = clubs.Where(c => !c.IsActive).ToList();
                    break;
                default: // manage
                    filteredList = clubs;
                    break;
            }

            var vm = new ClubDashboardViewModel
            {
                TableData = filteredList,
                ShowToggle = filter.ToLower() == "manage",
                TotalCount = clubs.Count,
                ActiveCount = clubs.Count(c => c.IsActive),
                InactiveCount = clubs.Count(c => !c.IsActive)
            };

            return View(vm);
        }

        // Toggle Club Status
        [HttpPost]
        public JsonResult ToggleClubStatus(int id, bool isActive)
        {
            if (Session["UserRole"]?.ToString() != "Director")
            {
                return Json(new { success = false, message = "Unauthorized" });
            }

            try
            {
                var club = _db.CLUBS.FirstOrDefault(c => c.ClubID == id);
                if (club == null)
                {
                    return Json(new { success = false, message = "Club not found" });
                }

                club.IsActive = isActive;
                _db.SaveChanges();

                return Json(new { success = true, message = "Club status updated successfully" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }



        // change password

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
            return RedirectToAction("ChangePassword", "Director"); // ✅ Redirecting to Mentor Dashboard
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

        public ActionResult EventsForDirectorApproval()
        {
            string email = Session["UserEmail"]?.ToString();

            var director = _db.Logins.FirstOrDefault(l => l.Email == email && l.Role == "Director");
            if (director == null)
                return HttpNotFound("Director not found");

            int departmentId = (int)director.DepartmentID;

            var activeClubs = _db.CLUBS
                .Where(c => c.DepartmentID == departmentId && c.ApprovalStatusID == 2)
                .Select(c => new SelectListItem
                {
                    Value = c.ClubID.ToString(),
                    Text = c.ClubName
                })
                .ToList();

            var clubIds = activeClubs.Select(c => int.Parse(c.Value)).ToList(); // ✅ Define clubIds here

            var events = _db.EVENTS
                .Include(e => e.CLUB)
                .Where(e => e.ApprovalStatusID == 7 && clubIds.Contains((int)e.ClubID))
                .ToList();

            var viewModel = new DirectorEventApprovalViewModel
            {
                ActiveClubs = activeClubs,
                Events = events,
                SelectedClubId = null // or set to filter specific club if needed
            };

            return View(viewModel);


        }






        // ===========================
        // GET: Reject Event (Director email link)
        // ===========================
        [HttpGet]
        [AllowAnonymous]
        public ActionResult DirectorRejectEvent(string token)
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

            var ev = _db.EVENTS.Include(e => e.CLUB).FirstOrDefault(e => e.EventID == eventId);
            if (ev == null)
                return HttpNotFound("Event not found");

            // Prevent rejection if already approved or rejected
            if (ev.ApprovalStatusID == 2)
                return Content($"Event '{ev.EventName}' has already been approved.");
            if (ev.ApprovalStatusID == 3)
                return Content($"Event '{ev.EventName}' has already been rejected.");

            ViewBag.Token = token; // pass token to form
            return View("DirectorRejectEvent", ev);
        }

        // ===========================
        // POST: Reject Event (Director dashboard or email)
        // ===========================
        [HttpPost]
        [AllowAnonymous] // allow both flows
        [ValidateAntiForgeryTokenIfNoToken] // custom attribute we'll define below
        public async Task<ActionResult> DirectorRejectEvent(int? eventId, string rejectionReason, string token = null)
        {
            try
            {
                int evtId;

                if (!string.IsNullOrEmpty(token))
                {
                    // Email link flow
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
                    if (parts.Length < 1)
                        return Json(new { success = false, message = "Invalid token data." });

                    evtId = Convert.ToInt32(parts[0]);
                }
                else if (eventId.HasValue)
                {
                    // In-app dashboard flow
                    evtId = eventId.Value;
                }
                else
                {
                    return Json(new { success = false, message = "Missing event information." });
                }

                // Fetch event with minimal required data
                var ev = _db.EVENTS
                    .Include(e => e.CLUB)
                    .FirstOrDefault(e => e.EventID == evtId);

                if (ev == null || ev.CLUB == null)
                    return Json(new { success = false, message = "Event not found." });

                // Prevent rejection if already approved or rejected
                if (ev.ApprovalStatusID == 2)
                    return Json(new { success = false, message = $"Event '{ev.EventName}' has already been approved." });
                if (ev.ApprovalStatusID == 3)
                    return Json(new { success = false, message = $"Event '{ev.EventName}' is already rejected." });

                // Update event
                ev.ApprovalStatusID = 3;
                ev.RejectionReason = string.IsNullOrWhiteSpace(rejectionReason) ? "No reason provided." : rejectionReason;

                string message = $"❌ Event '{ev.EventName}' from club '{ev.CLUB.ClubName}' was rejected by Director.\nReason: {ev.RejectionReason}";
                var emailService = new EmailService();
                var emailTasks = new List<Task>();

                // 1️⃣ Notify Mentor
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

                // 2️⃣ Notify Club Admin
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

                // 3️⃣ Notify SubHOD via SUBDEPARTMENT
                if (ev.CLUB.SubDepartmentID != null && ev.CLUB.SubDepartmentID != 0)
                {
                    var subDept = _db.SUBDEPARTMENTs.FirstOrDefault(sd => sd.SubDepartmentID == ev.CLUB.SubDepartmentID);
                    if (subDept != null && !string.IsNullOrEmpty(subDept.HOD_Email))
                    {
                        var subHODLogin = _db.Logins.FirstOrDefault(l => l.Email == subDept.HOD_Email);
                        if (subHODLogin != null)
                        {
                            _db.Notifications.Add(new Notification
                            {
                                LoginID = subHODLogin.LoginID,
                                Message = message,
                                IsRead = false,
                                StartDate = DateTime.Now,
                                EndDate = DateTime.Now.AddDays(7),
                                CreatedDate = DateTime.Now
                            });

                            emailTasks.Add(emailService.SendEmailAsync(subHODLogin.Email, $"Event Rejected: {ev.EventName}", message));
                        }
                    }
                }

                // ✅ Save everything in one go
                _db.SaveChanges();

                // ✅ Send all emails in parallel
                if (emailTasks.Any())
                    await Task.WhenAll(emailTasks);

                // Decide response
                if (!string.IsNullOrEmpty(token))
                    return Content("✅ Event rejected successfully.");
                else
                {
                    TempData["Message"] = "Event rejected successfully.";
                    return RedirectToAction("EventsForDirectorApproval");
                }
            }
            catch (Exception ex)
            {
                if (!string.IsNullOrEmpty(token))
                    return Content("❌ Error: " + ex.Message);

                TempData["Error"] = ex.Message;
                return RedirectToAction("EventsForDirectorApproval");
            }
        }







        // ============================
        // Director Approve Event (GET via email link)
        // ============================
        [HttpGet]
        [AllowAnonymous] // allow approval without login for email flow
        public async Task<ActionResult> DirectorApproveEvent(string token)
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
            if (parts.Length < 1)
                return Content("❌ Invalid token data.");

            int evtId = Convert.ToInt32(parts[0]);

            var ev = _db.EVENTS.Include(e => e.CLUB).FirstOrDefault(e => e.EventID == evtId);
            if (ev == null || ev.CLUB == null)
                return Content("❌ Event not found.");

            // prevent duplicate approval
            if (ev.ApprovalStatusID == 2)
                return Content("✅ Event already approved.");
            if (ev.ApprovalStatusID == 3)
                return Content("❌ Event already rejected.");

            // full approval (no budget reduction in email flow)
            decimal proposedBudget = 0;
            decimal.TryParse(ev.EventBudget, out proposedBudget);

            ev.ApprovedAmount = proposedBudget.ToString();
            ev.ApprovalStatusID = 2;
            _db.SaveChanges();

            // Send notifications + emails
            await SendDirectorApprovalNotificationsAndEmails(ev, false, proposedBudget);

            return Content($"✅ Event '{ev.EventName}' ({ev.CLUB.ClubName}) was fully approved by Director.");
        }


        // ============================
        // Director Approve Event (POST in-app dashboard)
        // ============================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DirectorApproveEvent(int eventId,
                                                  decimal? approvedAmount,
                                                  HttpPostedFileBase signedDocument)
        {
            var ev = _db.EVENTS.Include(e => e.CLUB).FirstOrDefault(e => e.EventID == eventId);
            if (ev == null) return HttpNotFound("Event not found.");

            if (signedDocument == null || signedDocument.ContentLength == 0)
            {
                TempData["Error"] = "Signed PDF is required.";
                return RedirectToAction("EventsForDirectorApproval", new { id = eventId });
            }
            if (!signedDocument.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = "Only PDF files are allowed.";
                return RedirectToAction("EventsForDirectorApproval", new { id = eventId });
            }

            // Save signed document
            string uploadsRoot = Server.MapPath("~/uploads");
            Directory.CreateDirectory(uploadsRoot);

            string fileName = $"DirectorApproval_{eventId}_{DateTime.Now:yyyyMMddHHmmss}.pdf";
            string fullPath = Path.Combine(uploadsRoot, fileName);
            signedDocument.SaveAs(fullPath);

            ev.EventFormPath = "/uploads/" + fileName;

            // Budget handling
            decimal proposedBudget = 0;
            decimal.TryParse(ev.EventBudget, out proposedBudget);

            decimal finalBudget = (approvedAmount.HasValue && approvedAmount.Value > 0)
                                  ? approvedAmount.Value
                                  : proposedBudget;

            ev.ApprovedAmount = finalBudget.ToString();

            bool budgetReduced = finalBudget < proposedBudget;

            ev.ApprovalStatusID = budgetReduced ? 6 : 2;

            _db.SaveChanges();

            // Send notifications + emails
            await SendDirectorApprovalNotificationsAndEmails(ev, budgetReduced, finalBudget);

            TempData["Message"] = "Event approved by Director successfully.";
            return RedirectToAction("EventsForDirectorApproval", new { id = eventId });
        }


        // ============================
        // Shared Method: Notifications + Emails
        // ============================
        private async Task SendDirectorApprovalNotificationsAndEmails(EVENT ev, bool budgetReduced, decimal finalBudget)
        {
            string baseMsg = $"✅ Event '{ev.EventName}' ({ev.CLUB.ClubName}) ";
            string notifMsg = budgetReduced
                ? baseMsg + $"was approved by Director with reduced budget ₹{finalBudget:N0}."
                : baseMsg + "has been fully approved by Director.";

            var emailService = new EmailService();

            // --- Mentor ---
            if (ev.CLUB.MentorID != null)
            {
                var mentorLogin = _db.Logins.FirstOrDefault(l => l.LoginID == ev.CLUB.MentorID);
                if (mentorLogin != null)
                {
                    _db.Notifications.Add(new Notification
                    {
                        LoginID = mentorLogin.LoginID,
                        Message = notifMsg,
                        IsRead = false,
                        StartDate = DateTime.Now,
                        EndDate = DateTime.Now.AddDays(7),
                        CreatedDate = DateTime.Now
                    });

                    if (!string.IsNullOrEmpty(mentorLogin.Email))
                        await emailService.SendEmailAsync(mentorLogin.Email, $"Event Approved: {ev.EventName}", notifMsg);
                }
            }

            // --- SubHOD ---
            if (ev.CLUB.SubDepartmentID != null && ev.CLUB.SubDepartmentID != 0)
            {
                var subDept = _db.SUBDEPARTMENTs.FirstOrDefault(sd => sd.SubDepartmentID == ev.CLUB.SubDepartmentID);
                if (subDept != null && !string.IsNullOrEmpty(subDept.HOD_Email))
                {
                    var subHODLogin = _db.Logins.FirstOrDefault(l => l.Email == subDept.HOD_Email);
                    if (subHODLogin != null)
                    {
                        _db.Notifications.Add(new Notification
                        {
                            LoginID = subHODLogin.LoginID,
                            Message = notifMsg,
                            IsRead = false,
                            StartDate = DateTime.Now,
                            EndDate = DateTime.Now.AddDays(7),
                            CreatedDate = DateTime.Now
                        });

                        await emailService.SendEmailAsync(subHODLogin.Email, $"Event Approved: {ev.EventName}", notifMsg);
                    }
                }
            }

            // --- Club Admin ---
            var clubAdminLogin = _db.Logins.FirstOrDefault(l => l.ClubID == ev.ClubID);
            if (clubAdminLogin != null)
            {
                _db.Notifications.Add(new Notification
                {
                    LoginID = clubAdminLogin.LoginID,
                    Message = notifMsg,
                    IsRead = false,
                    StartDate = DateTime.Now,
                    EndDate = DateTime.Now.AddDays(7),
                    CreatedDate = DateTime.Now
                });

                if (!string.IsNullOrEmpty(clubAdminLogin.Email))
                    await emailService.SendEmailAsync(clubAdminLogin.Email, $"Event Approved: {ev.EventName}", notifMsg);
            }

            _db.SaveChanges();
        }



    }

}