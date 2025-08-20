using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using WebApplication4.Models;
using System.Data.Entity; // ✅ Required for Include()


namespace WebApplication4.Controllers
{
    public class SubHODController : Controller
    {
        private readonly dummyclubsEntities _db = new dummyclubsEntities();
        private readonly EmailService _emailService = new EmailService();  // Injecting EmailService
                                                                           // GET: SubHOD
        public ActionResult Index()
        {
            if (Session["UserRole"]?.ToString() != "SubHOD")
            {
                return RedirectToAction("Login", "Admin");
            }

            string subHODEmail = Session["UserEmail"]?.ToString();

            var subDepartments = _db.SUBDEPARTMENTs
                                    .Where(s => s.HOD_Email == subHODEmail)
                                    .ToList();

            if (!subDepartments.Any())
            {
                ViewBag.Message = "No sub-departments found for this Sub HOD.";
                return View();
            }

            var firstSubDept = subDepartments.First();
            var department = _db.DEPARTMENTs.FirstOrDefault(d => d.DepartmentID == firstSubDept.DepartmentID);
            var university = _db.UNIVERSITies.FirstOrDefault(u => u.UniversityID == department.Universityid);

            ViewBag.SubHODName = firstSubDept.HOD;
            ViewBag.DepartmentName = department?.DepartmentName;
            ViewBag.DirectorName = department?.DirectorName;
            ViewBag.UniversityName = university?.UniversityNAME;

            // Mentor statistics
            var mentorEmails = subDepartments.Select(sd => sd.SubDepartmentID).ToList();

            var mentors = _db.Logins
                            .Where(l => l.Role == "Mentor" && l.SubDepartmentID != null && mentorEmails.Contains((int)l.SubDepartmentID))
                            .ToList();

            ViewBag.TotalMentors = mentors.Count;
            ViewBag.ActiveMentors = mentors.Count(m => m.IsActive == true);
            ViewBag.DeactivatedMentors = mentors.Count(m => m.IsActive == false);

            // Clubs under SubHOD
            var clubs = _db.CLUBS
                            .Where(c => c.SubDepartmentID != null && mentorEmails.Contains((int)c.SubDepartmentID))
                            .ToList();

            ViewBag.TotalClubs = clubs.Count;
            ViewBag.ActiveClubs = clubs.Count(c => c.IsActive == true);
            ViewBag.DeactivatedClubs = clubs.Count(c => c.IsActive == false);

            return View(subDepartments);
        }





        [HttpGet]
        public ActionResult AddMentor()
        {
            var model = new USER();

            // Get all SubDepartments under the logged-in SubHOD
            string email = Session["UserEmail"]?.ToString();
            var subDepartments = _db.SUBDEPARTMENTs
                .Where(s => s.HOD_Email == email)
                .ToList();

            // Pass list to view as dropdown
            ViewBag.SubDepartments = new SelectList(subDepartments, "SubDepartmentID", "SubDepartmentName");

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> AddMentor(USER model, HttpPostedFileBase Photo)
        {
            if (!ModelState.IsValid)
            {
                // Repopulate dropdown on validation failure
                string email = Session["UserEmail"]?.ToString();
                var subDepartments = _db.SUBDEPARTMENTs
                    .Where(s => s.HOD_Email == email)
                    .ToList();
                ViewBag.SubDepartments = new SelectList(subDepartments, "SubDepartmentID", "SubDepartmentName");

                return View(model);
            }

            try
            {
                // ✅ Save uploaded photo
                if (Photo != null && Photo.ContentLength > 0)
                {
                    string uploadDir = Server.MapPath("~/Uploads/");
                    if (!Directory.Exists(uploadDir))
                        Directory.CreateDirectory(uploadDir);

                    string fileName = Path.GetFileName(Photo.FileName);
                    string path = Path.Combine(uploadDir, fileName);
                    Photo.SaveAs(path);
                    model.PhotoPath = "~/Uploads/" + fileName;
                }

                // ✅ Set mentor user properties
                model.RegistrationDate = DateTime.Now;
                model.IsActiveDate = DateTime.Now;
                model.IsActive = true;
                model.Userrole = "Mentor";
                model.UserType = "Campus";
                model.SubscriptionStatus = "Normal";

                // ✅ Assign DepartmentID from SubDepartment
                if (model.SubDepartmentID != null)
                {
                    var subDept = _db.SUBDEPARTMENTs.FirstOrDefault(s => s.SubDepartmentID == model.SubDepartmentID);
                    if (subDept != null)
                    {
                        model.DepartmentID = subDept.DepartmentID; // <-- assign here
                    }
                }

                _db.USERs.Add(model);
                await _db.SaveChangesAsync();

                // ✅ Fetch UniversityID and DepartmentID using SubDepartmentID
                int? universityId = null;
                int? departmentId = null;

                if (model.SubDepartmentID != null)
                {
                    var subDept = _db.SUBDEPARTMENTs.FirstOrDefault(s => s.SubDepartmentID == model.SubDepartmentID);
                    if (subDept != null)
                    {
                        departmentId = subDept.DepartmentID;

                        var dept = _db.DEPARTMENTs.FirstOrDefault(d => d.DepartmentID == departmentId);
                        universityId = dept?.Universityid;
                    }
                }

                // ✅ Create login record with SubDepartmentID, DepartmentID, and UniversityID
                var login = new Models.Login
                {
                    Email = model.Email,
                    PasswordHash = "Mentor@123", // Note: Use hash in production
                    Role = "Mentor",
                    CreatedDate = DateTime.Now,
                    IsActive = true,
                    SubDepartmentID = model.SubDepartmentID,
                    DepartmentID = departmentId,
                    UniversityID = universityId
                };

                _db.Logins.Add(login);
                await _db.SaveChangesAsync();

                // ✅ Send welcome email
                string subject = "Welcome to Our Platform!";
                string body = $"Hello {model.FirstName},<br/><br/>" +
                              $"You have been successfully added as a <strong>Mentor</strong>.<br/>" +
                              $"<strong>Login Email:</strong> {model.Email}<br/>" +
                              $"<strong>Temporary Password:</strong> Mentor@123<br/><br/>" +
                              $"<em>Please change your password after logging in for security purposes.</em><br/><br/>" +
                              $"Thank you!";
                await _emailService.SendEmailAsync(model.Email, subject, body);

                // ✅ Success message
                TempData["SuccessMessage"] = $"Mentor added successfully. " +
                                             $"Login credentials have been sent to <strong>{model.Email}</strong>.";

                return RedirectToAction("AddMentor", "SubHOD");
            }
            catch (Exception ex)
            {
                ViewBag.ErrorMessage = "Error: " + ex.Message;

                // Repopulate dropdown on exception
                string email = Session["UserEmail"]?.ToString();
                var subDepartments = _db.SUBDEPARTMENTs
                    .Where(s => s.HOD_Email == email)
                    .ToList();
                ViewBag.SubDepartments = new SelectList(subDepartments, "SubDepartmentID", "SubDepartmentName");

                return View(model);
            }
        }

        public ActionResult ManageMentors()
        {
            string subHODEmail = Session["UserEmail"]?.ToString();
            var subDept = _db.SUBDEPARTMENTs.FirstOrDefault(sd => sd.HOD_Email == subHODEmail);

            if (subDept == null)
                return HttpNotFound("SubDepartment not found");

            int subDeptId = subDept.SubDepartmentID;

            // Fetch raw data first (without conversion)
            var mentorData = (from login in _db.Logins
                              where login.Role == "Mentor" && login.SubDepartmentID == subDeptId
                              join user in _db.USERs on login.Email equals user.Email
                              select new
                              {
                                  login.LoginID,
                                  user.FirstName,
                                  user.LastName,
                                  login.Email,
                                  user.MobileNumber,
                                  login.IsActive
                              }).ToList();

            // Now do the conversion in memory (outside DB)
            var mentors = mentorData.Select(m => new MentorViewModel
            {
                LoginID = m.LoginID,
                FullName = m.FirstName + " " + m.LastName,
                Email = m.Email,
                Mobile = m.MobileNumber,
                IsActive = m.IsActive == true // null-safe comparison
            }).ToList();

            return View(mentors);
        }


        public ActionResult ActivateMentor(int id)
        {
            var login = _db.Logins.Find(id);
            if (login != null)
            {
                login.IsActive = true;
                _db.SaveChanges();
            }
            return RedirectToAction("ManageMentors");
        }

        public ActionResult DeactivateMentor(int id)
        {
            var login = _db.Logins.Find(id);
            if (login != null)
            {
                login.IsActive = false;
                _db.SaveChanges();
            }
            return RedirectToAction("ManageMentors");
        }

        public ActionResult ViewMentors()
        {
            string subHODEmail = Session["UserEmail"]?.ToString();
            var subDept = _db.SUBDEPARTMENTs.FirstOrDefault(sd => sd.HOD_Email == subHODEmail);

            if (subDept == null)
                return HttpNotFound("SubDepartment not found");

            int subDeptId = subDept.SubDepartmentID;

            var mentors = (from login in _db.Logins
                           where login.Role == "Mentor" && login.SubDepartmentID == subDeptId
                           join user in _db.USERs on login.Email equals user.Email
                           select new MentorViewModel
                           {
                               LoginID = login.LoginID,
                               FullName = user.FirstName + " " + user.LastName,
                               Email = login.Email,
                               Mobile = user.MobileNumber,
                               IsActive = login.IsActive ?? false
                           }).ToList();

            ViewBag.TotalCount = mentors.Count;
            ViewBag.ActiveCount = mentors.Count(m => m.IsActive);
            ViewBag.InactiveCount = mentors.Count(m => !m.IsActive);

            return View(mentors);
        }



        // ✅ CLUB REQUESTS
        private bool IsSubHODLoggedIn()
        {
            return Session["SubHOD_LoginID"] != null;
        }

        private int GetLoginID()
        {
            return Convert.ToInt32(Session["SubHOD_LoginID"]);
        }



        public ActionResult ClubRequests()
        {
            if (!IsSubHODLoggedIn())
            {
                TempData["ErrorMessage"] = "Not logged in as SubHOD.";
                return RedirectToAction("Index", "SubHOD");
            }

            int subHodLoginID = GetLoginID();

            var subHodEmail = _db.Logins
                .Where(l => l.LoginID == subHodLoginID)
                .Select(l => l.Email)
                .FirstOrDefault();

            if (string.IsNullOrEmpty(subHodEmail))
            {
                TempData["ErrorMessage"] = "SubHOD email not found.";
                return RedirectToAction("Index", "SubHOD");
            }

            var subDepartmentIDs = _db.SUBDEPARTMENTs
                .Where(sd => sd.HOD_Email.ToLower() == subHodEmail.ToLower())
                .Select(sd => sd.SubDepartmentID)
                .ToList();

            if (!subDepartmentIDs.Any())
            {
                TempData["ErrorMessage"] = "No SubDepartments assigned to this SubHOD.";
                return RedirectToAction("Index", "SubHOD");
            }
            var clubs = _db.CLUBS
                .Where(c => c.SubDepartmentID.HasValue && subDepartmentIDs.Contains(c.SubDepartmentID.Value))
                .Include(c => c.SUBDEPARTMENT)
                .Include(c => c.SUBDEPARTMENT.DEPARTMENT)
                .Include(c => c.SUBDEPARTMENT.DEPARTMENT.UNIVERSITY)
                .ToList();

            // ✅ Fill mentor name separately
            foreach (var club in clubs)
            {
                var mentorLogin = _db.Logins
                    .FirstOrDefault(l => l.SubDepartmentID == club.SubDepartmentID && l.Role == "Mentor");

                if (mentorLogin != null)
                {
                    var mentorUser = _db.USERs.FirstOrDefault(u => u.Email.ToLower() == mentorLogin.Email.ToLower());
                    club.MentorName = mentorUser != null
                        ? mentorUser.FirstName + " " + mentorUser.LastName
                        : "Mentor (User Not Found)";
                }
                else
                {
                    club.MentorName = "Mentor Not Assigned";
                }
            }

            return View(clubs);
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ApproveClub(int id)
        {
            var club = _db.CLUBS.Find(id);
            if (club != null)
            {
                club.ApprovalStatusID = 2; // Approved
                club.IsActive = true;
                _db.SaveChanges();

                var notification = new Notification
                {
                    LoginID = club.MentorID,
                    Message = $"✅ Your club '{club.ClubName}' has been approved by Sub-HOD!",
                    IsRead = false,
                    StartDate = DateTime.Now,
                    EndDate = DateTime.Now.AddDays(7),
                    CreatedDate = DateTime.Now
                };

                _db.Notifications.Add(notification);
                _db.SaveChanges();
            }

            return Json(new { success = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RejectClub(int id, string reason)
        {
            var club = _db.CLUBS.Find(id);
            if (club != null)
            {
                club.ApprovalStatusID = 3;
                _db.SaveChanges();

                if (string.IsNullOrWhiteSpace(reason))
                    reason = "No specific reason provided.";

                if (club.MentorID != null)
                {
                    var notification = new Notification
                    {
                        LoginID = club.MentorID,
                        Message = $"❌ Your club '{club.ClubName}' was rejected by Sub-HOD.\nReason: {reason}",
                        IsRead = false,
                        StartDate = DateTime.Now,
                        EndDate = DateTime.Now.AddDays(7),
                        CreatedDate = DateTime.Now
                    };

                    _db.Notifications.Add(notification);
                    _db.SaveChanges();
                }

                return Json(new { success = true });
            }

            return Json(new { success = false, message = "Club not found!" });
        }


        //ViewEventRequests
        public ActionResult ViewEventRequests()
        {
            if (Session["UserEmail"] == null)
                return RedirectToAction("Login", "Admin");

            string subHODEmail = Session["UserEmail"].ToString();

            var subDept = _db.SUBDEPARTMENTs.FirstOrDefault(sd => sd.HOD_Email == subHODEmail);
            if (subDept == null)
            {
                TempData["ErrorMessage"] = "Sub-department not found.";
                return RedirectToAction("Index", "Home");
            }

            var activeClubs = _db.CLUBS
                .Where(c => c.SubDepartmentID == subDept.SubDepartmentID && c.IsActive == true)
                .Select(c => new SelectListItem
                {
                    Value = c.ClubID.ToString(),
                    Text = c.ClubName
                }).ToList();

            var model = new SubHODEventReviewViewModel
            {
                ActiveClubs = activeClubs
            };

            return View(model);
        }

        [HttpPost]
        public ActionResult ViewEventRequests(SubHODEventReviewViewModel model)
        {
            if (Session["UserEmail"] == null)
                return RedirectToAction("Login", "Admin");

            string subHODEmail = Session["UserEmail"].ToString();
            var subDept = _db.SUBDEPARTMENTs.FirstOrDefault(sd => sd.HOD_Email == subHODEmail);

            if (subDept == null)
            {
                TempData["ErrorMessage"] = "Sub-department not found.";
                return RedirectToAction("Index", "Home");
            }

            // Repopulate dropdown after postback
            model.ActiveClubs = _db.CLUBS
                .Where(c => c.SubDepartmentID == subDept.SubDepartmentID && c.ApprovalStatusID == 2)
                .Select(c => new SelectListItem
                {
                    Value = c.ClubID.ToString(),
                    Text = c.ClubName
                }).ToList();

            // Load events forwarded to SubHOD (ApprovalStatus = 4)
            model.Events = _db.EVENTS
                .Where(e => e.ClubID == model.SelectedClubId && e.ApprovalStatusID == 4)
                .ToList();

            return View(model);
        }

        [HttpPost]
        public ActionResult ForwardToDirector(int eventId)
        {
            var evt = _db.EVENTS.Find(eventId);
            if (evt == null)
                return HttpNotFound();

            // Update status to "Forwarded to Director" (let's say ID = 4)
            evt.ApprovalStatusID = 7;
            _db.SaveChanges();

            TempData["SuccessMessage"] = "Event forwarded to Director successfully.";
            return RedirectToAction("ViewEventRequests");
        }

        [HttpPost]
        public ActionResult RejectEvent(int EventID, string RejectionReason)
        {
            var evt = _db.EVENTS.Find(EventID);
            if (evt == null)
                return HttpNotFound();

            evt.ApprovalStatusID = 3; // Assuming 5 = Rejected
            evt.RejectionReason = RejectionReason;
            _db.SaveChanges();

            TempData["SuccessMessage"] = "Event rejected successfully.";
            return RedirectToAction("ViewEventRequests");
        }


        //clubs managing and viewing 

        [HttpGet]
        public ActionResult ManageClubs() // This loads the view
        {
            if (Session["UserEmail"] == null)
                return RedirectToAction("Login", "Admin");

            return View();
        }

        [HttpGet]
        public ActionResult GetClubs()
        {
            if (Session["UserEmail"] == null)
                return Json(new { error = "Not logged in" }, JsonRequestBehavior.AllowGet);

            string subHODEmail = Session["UserEmail"].ToString();
            var subDept = _db.SUBDEPARTMENTs.FirstOrDefault(sd => sd.HOD_Email == subHODEmail);

            if (subDept == null)
                return Json(new { error = "Sub-department not found" }, JsonRequestBehavior.AllowGet);

            int subDeptId = subDept.SubDepartmentID;

            var clubs = (from c in _db.CLUBS
                         join d in _db.DEPARTMENTs on c.DepartmentID equals d.DepartmentID
                         join sd in _db.SUBDEPARTMENTs on c.SubDepartmentID equals sd.SubDepartmentID
                         where c.SubDepartmentID == subDeptId
                         select new
                         {
                             c.ClubID,
                             c.ClubName,
                             c.IsActive,
                             DepartmentName = d.DepartmentName,
                             SubDepartmentName = sd.SubDepartmentName
                         }).ToList();

            var result = new
            {
                TotalClubs = clubs.Count,
                ActiveClubs = clubs.Count(c => c.IsActive == true),
                InactiveClubs = clubs.Count(c => !c.IsActive == true),
                Clubs = clubs
            };

            return Json(result, JsonRequestBehavior.AllowGet);
        }



        [HttpPost]
        public ActionResult ToggleClubStatus(int clubId)
        {
            if (Session["UserEmail"] == null)
                return Json(new { success = false, message = "Not logged in." });

            // Get the logged-in Sub HOD's email
            string subHODEmail = Session["UserEmail"].ToString();

            // Ensure this Sub HOD owns the club's sub-department
            var subDept = _db.SUBDEPARTMENTs
                .FirstOrDefault(sd => sd.HOD_Email == subHODEmail);

            if (subDept == null)
                return Json(new { success = false, message = "Unauthorized: Sub-department not found." });

            // Find the club under that sub-department
            var club = _db.CLUBS.FirstOrDefault(c => c.ClubID == clubId && c.SubDepartmentID == subDept.SubDepartmentID);

            if (club == null)
                return Json(new { success = false, message = "Club not found or not under your sub-department." });

            // Toggle the IsActive value safely
            club.IsActive = !(club.IsActive ?? false);

            _db.SaveChanges();

            return Json(new
            {
                success = true,
                newStatus = club.IsActive,
                message = $"Club '{club.ClubName}' is now {(club.IsActive == true ? "Active" : "Inactive")}."
            });
        }


        [HttpGet]
        public ActionResult ViewClubs()
        {
            if (Session["UserEmail"] == null)
                return RedirectToAction("Login", "Admin");

            return View(); // This loads your Razor view (HTML)
        }

        [HttpGet]
        public ActionResult GettClubs()
        {
            if (Session["UserEmail"] == null)
                return Json(new { error = "Not logged in" }, JsonRequestBehavior.AllowGet);

            string subHODEmail = Session["UserEmail"].ToString();
            var subDept = _db.SUBDEPARTMENTs.FirstOrDefault(sd => sd.HOD_Email == subHODEmail);

            if (subDept == null)
                return Json(new { error = "Sub-department not found" }, JsonRequestBehavior.AllowGet);

            int subDeptId = subDept.SubDepartmentID;

            var clubs = (from c in _db.CLUBS
                         join d in _db.DEPARTMENTs on c.DepartmentID equals d.DepartmentID
                         join sd in _db.SUBDEPARTMENTs on c.SubDepartmentID equals sd.SubDepartmentID
                         where c.SubDepartmentID == subDeptId
                         select new
                         {
                             c.ClubID,
                             c.ClubName,
                             c.IsActive,
                             DepartmentName = d.DepartmentName,
                             SubDepartmentName = sd.SubDepartmentName
                         }).ToList();

            var result = new
            {
                TotalClubs = clubs.Count,
                ActiveClubs = clubs.Count(c => c.IsActive == true),
                InactiveClubs = clubs.Count(c => c.IsActive == false),
                Clubs = clubs
            };

            return Json(result, JsonRequestBehavior.AllowGet);
        }




        // Change password and forget password 

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
            return RedirectToAction("ChangePassword", "SUBHOD"); // ✅ Redirecting to Mentor Dashboard
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
