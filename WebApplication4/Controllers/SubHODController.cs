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
    public class SubHODController : BaseController
    {
        private readonly dummyclubsEntities _db = new dummyclubsEntities();
        private readonly EmailService _emailService = new EmailService();  // Injecting EmailService
                                                                           // GET: SubHOD
        public ActionResult Index()
        {
            if (Session["Role"]?.ToString() != "SubHOD")
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
                if (string.IsNullOrEmpty(model.Email))
                    throw new Exception("Email is required.");

                int? deptId = null;
                int? universityId = null;

                // Assign DepartmentID and UniversityID from SubDepartment
                if (model.SubDepartmentID != null)
                {
                    var subDept = _db.SUBDEPARTMENTs.FirstOrDefault(s => s.SubDepartmentID == model.SubDepartmentID);
                    if (subDept != null)
                    {
                        deptId = subDept.DepartmentID;
                        model.DepartmentID = deptId;

                        var dept = _db.DEPARTMENTs.FirstOrDefault(d => d.DepartmentID == deptId);
                        universityId = dept?.Universityid;
                    }
                }

                // Handle photo upload
                if (Photo != null && Photo.ContentLength > 0)
                {
                    string uploadDir = Server.MapPath("~/Uploads/");
                    if (!Directory.Exists(uploadDir))
                        Directory.CreateDirectory(uploadDir);

                    string fileName = Guid.NewGuid() + Path.GetExtension(Photo.FileName);
                    string path = Path.Combine(uploadDir, fileName);
                    Photo.SaveAs(path);
                    model.PhotoPath = "~/Uploads/" + fileName;
                }

                // Check if user exists
                var existingUser = _db.USERs.FirstOrDefault(u => u.Email.ToLower() == model.Email.ToLower());
                var existingLogin = _db.Logins.FirstOrDefault(l => l.Email.ToLower() == model.Email.ToLower());

                if (existingUser != null)
                {
                    // Update existing USER
                    existingUser.FirstName = model.FirstName;
                    existingUser.LastName = model.LastName;
                    existingUser.MobileNumber = model.MobileNumber;
                    existingUser.PhotoPath = model.PhotoPath ?? existingUser.PhotoPath;
                    existingUser.IsActive = true;
                    existingUser.UserType = model.UserType ?? existingUser.UserType;
                    existingUser.RegistrationDate = DateTime.Now;
                    existingUser.IsActiveDate = DateTime.Now;
                    existingUser.SubDepartmentID = model.SubDepartmentID;
                    existingUser.DepartmentID = deptId;

                    // Update roles
                    var roles = existingUser.Userrole?.Split(',').Select(r => r.Trim()).ToList() ?? new List<string>();
                    if (!roles.Contains("Mentor"))
                        roles.Add("Mentor");
                    existingUser.Userrole = string.Join(",", roles);
                }
                else
                {
                    // New USER
                    model.Userrole = "Mentor";
                    model.UserType = "Campus";
                    model.RegistrationDate = DateTime.Now;
                    model.IsActiveDate = DateTime.Now;
                    model.IsActive = true;

                    _db.USERs.Add(model);
                }

                if (existingLogin != null)
                {
                    // Update existing Login
                    var loginRoles = existingLogin.Role?.Split(',').Select(r => r.Trim()).ToList() ?? new List<string>();
                    if (!loginRoles.Contains("Mentor"))
                        loginRoles.Add("Mentor");

                    existingLogin.Role = string.Join(",", loginRoles);
                    existingLogin.DepartmentID = deptId;
                    existingLogin.UniversityID = universityId;
                    existingLogin.SubDepartmentID = model.SubDepartmentID;
                }
                else
                {
                    // New Login
                    var login = new Login
                    {
                        Email = model.Email,
                        PasswordHash = "Mentor@123", // Use hashed password in production
                        Role = "Mentor",
                        CreatedDate = DateTime.Now,
                        IsActive = true,
                        DepartmentID = deptId,
                        UniversityID = universityId,
                        SubDepartmentID = model.SubDepartmentID
                    };
                    _db.Logins.Add(login);
                }

                // Save all changes
                await _db.SaveChangesAsync();

                // Send welcome email (optional)
                try
                {
                    string subject = "Welcome to Our Platform!";
                    string body = $"Hello {model.FirstName},<br/><br/>" +
                                  $"You have been added/updated as a <strong>Mentor</strong>.<br/>" +
                                  $"<strong>Login Email:</strong> {model.Email}<br/>" +
                                  $"<strong>Temporary Password:</strong> Mentor@123<br/><br/>" +
                                  $"<em>Please change your password after logging in.</em>";
                    await _emailService.SendEmailAsync(model.Email, subject, body);
                }
                catch { /* log if needed */ }

                TempData["SuccessMessage"] = $"Mentor details saved successfully. Login info sent to <strong>{model.Email}</strong>.";
                return RedirectToAction("AddMentor", "SubHOD");
            }
            catch (Exception ex)
            {
                ViewBag.ErrorMessage = "Error: " + ex.Message + " " + ex.InnerException?.Message;

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
            string userEmail = Session["UserEmail"]?.ToString().ToLower();

            // 1️⃣ Get all sub-department IDs where the user is HOD
            var subDeptIdsAsHOD = _db.SUBDEPARTMENTs
                .Where(sd => sd.HOD_Email.ToLower() == userEmail)
                .Select(sd => sd.SubDepartmentID)
                .ToList();

            // 2️⃣ Get all sub-department IDs where the user is a Mentor
            var subDeptIdsAsMentor = _db.Logins
                .Where(l => l.Email.ToLower() == userEmail && l.Role == "Mentor")
                .Select(l => l.SubDepartmentID)
                .Where(id => id.HasValue)
                .Select(id => id.Value)
                .ToList();

            // 3️⃣ Merge all sub-department IDs (avoid duplicates)
            var allSubDeptIds = subDeptIdsAsHOD.Union(subDeptIdsAsMentor).ToList();

            if (!allSubDeptIds.Any())
                return HttpNotFound("No SubDepartments found for this user.");

            // 4️⃣ Fetch mentors for all relevant subdepartments
            var mentorData = (from login in _db.Logins
                              where login.Role == "Mentor"
                                    && login.SubDepartmentID.HasValue
                                    && allSubDeptIds.Contains(login.SubDepartmentID.Value)
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

            var mentors = mentorData.Select(m => new MentorViewModel
            {
                LoginID = m.LoginID,
                FullName = m.FirstName + " " + m.LastName,
                Email = m.Email,
                Mobile = m.MobileNumber,
                IsActive = m.IsActive == true
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
            string userEmail = Session["UserEmail"]?.ToString().ToLower();

            var subDeptIdsAsHOD = _db.SUBDEPARTMENTs
                .Where(sd => sd.HOD_Email.ToLower() == userEmail)
                .Select(sd => sd.SubDepartmentID)
                .ToList();

            var subDeptIdsAsMentor = _db.Logins
                .Where(l => l.Email.ToLower() == userEmail && l.Role == "Mentor")
                .Select(l => l.SubDepartmentID)
                .Where(id => id.HasValue)
                .Select(id => id.Value)
                .ToList();

            var allSubDeptIds = subDeptIdsAsHOD.Union(subDeptIdsAsMentor).ToList();

            if (!allSubDeptIds.Any())
                return HttpNotFound("No SubDepartments found for this user.");

            var mentors = (from login in _db.Logins
                           where login.Role == "Mentor"
                                 && login.SubDepartmentID.HasValue
                                 && allSubDeptIds.Contains(login.SubDepartmentID.Value)
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

            // Get all subdepartments of this SubHOD
            var subDepartmentIDs = _db.SUBDEPARTMENTs
                .Where(sd => sd.HOD_Email.ToLower() == subHodEmail.ToLower())
                .Select(sd => sd.SubDepartmentID)
                .ToList();

            if (!subDepartmentIDs.Any())
            {
                TempData["ErrorMessage"] = "No SubDepartments assigned to this SubHOD.";
                return RedirectToAction("Index", "SubHOD");
            }

            // Fetch all clubs for these subdepartments
            var clubs = _db.CLUBS
                .Where(c => c.SubDepartmentID.HasValue && subDepartmentIDs.Contains(c.SubDepartmentID.Value))
                .Include(c => c.SUBDEPARTMENT)
                .Include(c => c.SUBDEPARTMENT.DEPARTMENT)
                .Include(c => c.SUBDEPARTMENT.DEPARTMENT.UNIVERSITY)
                .ToList();

            // Fetch all mentors for these subdepartments
            var mentors = _db.Logins
                .Where(l => l.Role == "Mentor" && l.SubDepartmentID.HasValue && subDepartmentIDs.Contains(l.SubDepartmentID.Value))
                .Join(_db.USERs,
                      l => l.Email.ToLower(),
                      u => u.Email.ToLower(),
                      (l, u) => new
                      {
                          l.SubDepartmentID,
                          FullName = u.FirstName + " " + u.LastName
                      })
                .ToList();

            // Assign mentors to clubs
            foreach (var club in clubs)
            {
                var clubMentors = mentors
                    .Where(m => m.SubDepartmentID == club.SubDepartmentID)
                    .Select(m => m.FullName)
                    .ToList();

                club.MentorName = clubMentors.Any()
                    ? string.Join(", ", clubMentors)
                    : "Mentor Not Assigned";
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


        // GET: ViewEventRequests
        public ActionResult ViewEventRequests()
        {
            if (Session["UserEmail"] == null)
                return RedirectToAction("Login", "Admin");

            string subHODEmail = Session["UserEmail"].ToString();

            // Get all subdepartment IDs for this SubHOD
            var subDeptIDs = _db.SUBDEPARTMENTs
                .Where(sd => sd.HOD_Email.ToLower() == subHODEmail.ToLower())
                .Select(sd => sd.SubDepartmentID)
                .ToList();

            if (!subDeptIDs.Any())
            {
                TempData["ErrorMessage"] = "No sub-departments assigned to this SubHOD.";
                return RedirectToAction("Index", "Home");
            }

            // Fetch all active clubs under these subdepartments
            var activeClubs = _db.CLUBS
.Where(c => c.IsActive == true
            && c.SubDepartmentID.HasValue
            && subDeptIDs.Contains(c.SubDepartmentID.Value))
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

        // POST: ViewEventRequests
        [HttpPost]
        public ActionResult ViewEventRequests(SubHODEventReviewViewModel model)
        {
            if (Session["UserEmail"] == null)
                return RedirectToAction("Login", "Admin");

            string subHODEmail = Session["UserEmail"].ToString();

            // Get all subdepartment IDs for this SubHOD
            var subDeptIDs = _db.SUBDEPARTMENTs
                .Where(sd => sd.HOD_Email.ToLower() == subHODEmail.ToLower())
                .Select(sd => sd.SubDepartmentID)
                .ToList();

            if (!subDeptIDs.Any())
            {
                TempData["ErrorMessage"] = "No sub-departments assigned to this SubHOD.";
                return RedirectToAction("Index", "Home");
            }

            // Repopulate dropdown for clubs in all subdepartments
            model.ActiveClubs = _db.CLUBS
                .Where(c => subDeptIDs.Contains(c.SubDepartmentID ?? 0) && c.ApprovalStatusID == 2)
                .Select(c => new SelectListItem
                {
                    Value = c.ClubID.ToString(),
                    Text = c.ClubName
                }).ToList();

            // Load events forwarded to SubHOD (ApprovalStatus = 4)
            model.Events = _db.EVENTS
                .Where(e => e.ApprovalStatusID == 4 && model.SelectedClubId != 0 &&
                            subDeptIDs.Contains(_db.CLUBS
                                .Where(c => c.ClubID == e.ClubID)
                                .Select(c => c.SubDepartmentID ?? 0)
                                .FirstOrDefault()))
                .ToList();

            return View(model);
        }







        // ===========================
        // GET: Forward from email link (SubHOD -> Director)
        // ===========================
        [HttpGet]
        [AllowAnonymous]
        public async Task<ActionResult> ForwardToDirector(string token)
        {
            if (string.IsNullOrEmpty(token)) return HttpNotFound("Token missing");

            string plainData;
            try { plainData = SecureHelper.Decrypt(token); }
            catch { return HttpNotFound("Invalid token"); }

            var parts = plainData.Split('|');
            if (parts.Length < 2) return HttpNotFound("Invalid token data");

            int eventId = Convert.ToInt32(parts[0]);

            var evt = _db.EVENTS.Find(eventId);
            if (evt == null) return HttpNotFound("Event not found");

            // 🚫 Prevent forwarding if already rejected or already forwarded
            if (evt.ApprovalStatusID == 3)
                return Content($"Event '{evt.EventName}' has been rejected. You cannot forward it now.");
            if (evt.ApprovalStatusID == 7)
                return Content($"Event '{evt.EventName}' is already forwarded to Director.");

            evt.ApprovalStatusID = 7; // ForwardedToDirector
            _db.SaveChanges();

            // Send notification/email to Director (same as your current code)
            var club = _db.CLUBS.Find(evt.ClubID);
            int deptId = club != null ? club.DepartmentID : 0;
            var director = _db.Logins.FirstOrDefault(l => l.Role == "Director" && l.DepartmentID == deptId);

            if (director != null)
            {
                _db.Notifications.Add(new Notification
                {
                    LoginID = director.LoginID,
                    Message = $"📬 Event '{evt.EventName}' has been forwarded by SubHOD for your review.",
                    IsRead = false,
                    StartDate = DateTime.Now,
                    EndDate = DateTime.Now.AddDays(7),
                    CreatedDate = DateTime.Now
                });
                _db.SaveChanges();

                string newToken = SecureHelper.Encrypt($"{evt.EventID}|{evt.ClubID}|email");

                var emailSvc = new EmailService();
                string scheme = Request.Url.Scheme;
                string host = Request.Url.Host;
                string port = Request.Url.IsDefaultPort ? "" : ":" + Request.Url.Port;
                string baseUrl = $"{scheme}://{host}{port}";
                string budgetUrl = string.IsNullOrWhiteSpace(evt.BudgetDocumentPath) ? null :
                    (evt.BudgetDocumentPath.StartsWith("http") ? evt.BudgetDocumentPath : baseUrl + evt.BudgetDocumentPath);

                // Inside both GET and POST ForwardToDirector methods
                string loginUrl = Url.Action("Login", "Admin", null, scheme);

                string emailBody = $@"
<div style='font-family:Arial,sans-serif;font-size:14px;color:#000;line-height:1.5;'>
    <p style='background:#f8f9fa;padding:10px;border:1px solid #ddd;border-radius:5px;'>
        <strong>ℹ️ Note:</strong> If you want to <b>reduce the budget</b> or 
        <b>upload the signed approval document</b>, please log in to your dashboard:<br/>
        <a href='{loginUrl}' 
           style='display:inline-block;margin-top:6px;padding:8px 12px;background:#007bff;
                  color:#fff;text-decoration:none;border-radius:4px;'>
           Go to Dashboard
        </a>
    </p>
    <hr/>
    <h3>New Event Forwarded for Approval</h3>
    <p><b>Club:</b> {HttpUtility.HtmlEncode(club?.ClubName)}</p>
    <p><b>Event:</b> {HttpUtility.HtmlEncode(evt.EventName)}</p>
    <p><b>Description:</b> {HttpUtility.HtmlEncode(evt.EventDescription)}</p>
    <p><b>Venue:</b> {HttpUtility.HtmlEncode(evt.Venue)}</p>
    <p><b>Dates:</b> {evt.EventStartDateAndTime:dd-MMM-yyyy} – {evt.EventEndDateAndTime:dd-MMM-yyyy}</p>
    <p><b>Budget:</b> {evt.EventBudget}</p>
    {(budgetUrl != null ? $"<p><b>Budget Document:</b> <a href='{budgetUrl}'>View</a></p>" : "")}
    <div style='margin-top:16px;'>
        <a href='{Url.Action("DirectorApproveEvent", "Director", new { token = newToken }, scheme)}'
           style='padding:10px 14px;background:#1e7e34;color:#fff;text-decoration:none;border-radius:4px;margin-right:8px;'>Approve</a>
        <a href='{Url.Action("DirectorRejectEvent", "Director", new { token = newToken }, scheme)}'
           style='padding:10px 14px;background:#dc3545;color:#fff;text-decoration:none;border-radius:4px;'>Reject</a>
    </div>
</div>";


                await emailSvc.SendEmailAsync(director.Email, $"Approval Needed: {evt.EventName}", emailBody);
            }

            return Content($"✅ Event '{evt.EventName}' forwarded to Director successfully!");
        }


        // ===========================
        // POST: Forward inside SubHOD dashboard
        // ===========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ForwardToDirector(int eventId)
        {
            var evt = _db.EVENTS.Find(eventId);
            if (evt == null)
                return HttpNotFound("Event not found");

            // 🚫 Prevent duplicate forwarding
            if (evt.ApprovalStatusID == 7)
            {
                TempData["ErrorMessage"] = $"Event '{evt.EventName}' is already forwarded to Director.";
                return RedirectToAction("ViewEventRequests");
            }

            // ✅ Update status
            evt.ApprovalStatusID = 7; // ForwardedToDirector
            _db.SaveChanges();

            // ✅ Get related club & department
            var club = _db.CLUBS.Find(evt.ClubID);
            var deptId = club?.DepartmentID ?? 0;
            var director = _db.Logins.FirstOrDefault(l => l.Role == "Director" && l.DepartmentID == deptId);

            if (director != null)
            {
                // In-app notification
                _db.Notifications.Add(new Notification
                {
                    LoginID = director.LoginID,
                    Message = $"📬 Event '{evt.EventName}' has been forwarded by SubHOD for your review.",
                    IsRead = false,
                    StartDate = DateTime.Now,
                    EndDate = DateTime.Now.AddDays(7),
                    CreatedDate = DateTime.Now
                });
                _db.SaveChanges();

                // 🔑 Generate token for Director actions
                string token = SecureHelper.Encrypt($"{evt.EventID}|{evt.ClubID}|email");

                // 📧 Email body with venue, budget, budget document
                string scheme = Request.Url.Scheme;
                string host = Request.Url.Host;
                string port = Request.Url.IsDefaultPort ? "" : ":" + Request.Url.Port;
                string baseUrl = $"{scheme}://{host}{port}";

                string budgetUrl = string.IsNullOrWhiteSpace(evt.BudgetDocumentPath)
                    ? null
                    : (evt.BudgetDocumentPath.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                        ? evt.BudgetDocumentPath
                        : baseUrl + evt.BudgetDocumentPath);

                // Inside both GET and POST ForwardToDirector methods
                string loginUrl = Url.Action("Login", "Admin", null, scheme);

                string emailBody = $@"
<div style='font-family:Arial,sans-serif;font-size:14px;color:#000;line-height:1.5;'>
    <p style='background:#f8f9fa;padding:10px;border:1px solid #ddd;border-radius:5px;'>
        <strong>ℹ️ Note:</strong> If you want to <b>reduce the budget</b> or 
        <b>upload the signed approval document</b>, please log in to your dashboard:<br/>
        <a href='{loginUrl}' 
           style='display:inline-block;margin-top:6px;padding:8px 12px;background:#007bff;
                  color:#fff;text-decoration:none;border-radius:4px;'>
           Go to Dashboard
        </a>
    </p>
    <hr/>
    <h3>New Event Forwarded for Approval</h3>
    <p><b>Club:</b> {HttpUtility.HtmlEncode(club?.ClubName)}</p>
    <p><b>Event:</b> {HttpUtility.HtmlEncode(evt.EventName)}</p>
    <p><b>Description:</b> {HttpUtility.HtmlEncode(evt.EventDescription)}</p>
    <p><b>Venue:</b> {HttpUtility.HtmlEncode(evt.Venue)}</p>
    <p><b>Dates:</b> {evt.EventStartDateAndTime:dd-MMM-yyyy} – {evt.EventEndDateAndTime:dd-MMM-yyyy}</p>
    <p><b>Budget:</b> {evt.EventBudget}</p>
    {(budgetUrl != null ? $"<p><b>Budget Document:</b> <a href='{budgetUrl}'>View</a></p>" : "")}
    <div style='margin-top:16px;'>
        <a href='{Url.Action("DirectorApproveEvent", "Director", new { token }, scheme)}'
           style='padding:10px 14px;background:#1e7e34;color:#fff;text-decoration:none;border-radius:4px;margin-right:8px;'>Approve</a>
        <a href='{Url.Action("DirectorRejectEvent", "Director", new { token }, scheme)}'
           style='padding:10px 14px;background:#dc3545;color:#fff;text-decoration:none;border-radius:4px;'>Reject</a>
    </div>
</div>";


                var emailSvc = new EmailService();
                await emailSvc.SendEmailAsync(director.Email, $"Approval Needed: {evt.EventName}", emailBody);
            }

            TempData["SuccessMessage"] = "Event forwarded to Director successfully.";
            return RedirectToAction("ViewEventRequests");
        }








        // ===========================
        // GET: Reject Event (SubHOD email link)
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
            if (parts.Length < 2)
                return HttpNotFound("Invalid token data");

            int eventId = Convert.ToInt32(parts[0]);

            bool fromEmail = parts.Length >= 3 && parts[2].ToLower() == "email";
            ViewBag.FromEmail = fromEmail;

            var ev = _db.EVENTS.Find(eventId);
            if (ev == null) return HttpNotFound("Event not found");

            // 🚫 Prevent rejection if already forwarded or rejected
            if (ev.ApprovalStatusID == 7)
                return Content($"Event '{ev.EventName}' has already been forwarded. You cannot reject it now.");
            if (ev.ApprovalStatusID == 3)
                return Content($"Event '{ev.EventName}' is already rejected.");

            // ✅ Send token & event to view
            ev.Token = token;
            return View("RejectEvent", ev);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AllowAnonymous] // allow both flows
        public async Task<ActionResult> RejectEvent(int? eventId, string rejectionReason, string token = null)
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

                var ev = _db.EVENTS.FirstOrDefault(e => e.EventID == evtId);
                if (ev == null)
                    return Json(new { success = false, message = "Event not found!" });

                // Prevent rejection if already forwarded or rejected
                if (ev.ApprovalStatusID == 7)
                    return Json(new { success = false, message = $"Event '{ev.EventName}' has already been forwarded. You cannot reject it now." });
                if (ev.ApprovalStatusID == 3)
                    return Json(new { success = false, message = $"Event '{ev.EventName}' is already rejected." });

                // Update event as Rejected
                ev.ApprovalStatusID = 3;
                ev.RejectionReason = string.IsNullOrWhiteSpace(rejectionReason) ? "No specific reason provided." : rejectionReason;
                _db.SaveChanges();

                var emailService = new EmailService();

                // In-app notification to event organizer
                if (ev.EventOrganizerID != null)
                {
                    _db.Notifications.Add(new Notification
                    {
                        LoginID = ev.EventOrganizerID,
                        Message = $"❌ Your event '{ev.EventName}' was rejected by SubHOD.\nReason: {ev.RejectionReason}",
                        IsRead = false,
                        StartDate = DateTime.Now,
                        EndDate = DateTime.Now.AddDays(7),
                        CreatedDate = DateTime.Now
                    });
                    _db.SaveChanges();
                }

                // Fetch club info
                var club = _db.CLUBS.FirstOrDefault(c => c.ClubID == ev.ClubID);

                // Email to Club Admin
                var clubAdminLogin = _db.Logins.FirstOrDefault(l => l.ClubID == ev.ClubID);
                if (clubAdminLogin != null && !string.IsNullOrEmpty(clubAdminLogin.Email))
                {
                    string subject = $"Event '{ev.EventName}' Rejected by SubHOD";
                    string body = $@"
<p>Your event <strong>{ev.EventName}</strong> has been 
   <span style='color:red;font-weight:bold;'>rejected</span> by SubHOD.</p>
<p><strong>Reason:</strong> {ev.RejectionReason}</p>
<br/>
<p>Regards,<br/>University Event Management System</p>";
                    await emailService.SendEmailAsync(clubAdminLogin.Email, subject, body);
                }

                // Email to Mentor
                if (club != null && club.MentorID != null)
                {
                    var mentorLogin = _db.Logins.FirstOrDefault(l => l.LoginID == club.MentorID);
                    if (mentorLogin != null && !string.IsNullOrEmpty(mentorLogin.Email))
                    {
                        string subject = $"Event '{ev.EventName}' Rejected by SubHOD";
                        string body = $@"
<p>The event <strong>{ev.EventName}</strong> you supervise has been 
   <span style='color:red;font-weight:bold;'>rejected</span> by SubHOD.</p>
<p><strong>Reason:</strong> {ev.RejectionReason}</p>
<br/>
<p>Regards,<br/>University Event Management System</p>";
                        await emailService.SendEmailAsync(mentorLogin.Email, subject, body);
                    }
                }

                return Json(new { success = true, message = "Event rejected and emails sent to mentor and club admin." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
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

            string userEmail = Session["UserEmail"].ToString().ToLower();

            // 1️⃣ Get sub-departments where user is SubHOD
            var subHodDeptIds = _db.SUBDEPARTMENTs
                .Where(sd => sd.HOD_Email.ToLower() == userEmail)
                .Select(sd => sd.SubDepartmentID)
                .ToList();

            // 2️⃣ Get sub-departments where user is Mentor
            var mentorDeptIds = _db.Logins
                .Where(l => l.Email.ToLower() == userEmail && l.Role == "Mentor" && l.SubDepartmentID.HasValue)
                .Select(l => l.SubDepartmentID.Value)
                .ToList();

            // 3️⃣ Combine both lists
            var allDeptIds = subHodDeptIds.Union(mentorDeptIds).ToList();

            if (!allDeptIds.Any())
                return Json(new { error = "No accessible subdepartments found for this user." }, JsonRequestBehavior.AllowGet);

            // 4️⃣ Fetch all clubs under any of these sub-departments
            var clubs = (from c in _db.CLUBS
                         join d in _db.DEPARTMENTs on c.DepartmentID equals d.DepartmentID
                         join sd in _db.SUBDEPARTMENTs on c.SubDepartmentID equals sd.SubDepartmentID
                         where c.SubDepartmentID.HasValue && allDeptIds.Contains(c.SubDepartmentID.Value)
                         select new
                         {
                             c.ClubID,
                             c.ClubName,
                             IsActive = c.IsActive ?? false,
                             DepartmentName = d.DepartmentName,
                             SubDepartmentName = sd.SubDepartmentName
                         }).ToList();

            var result = new
            {
                TotalClubs = clubs.Count,
                ActiveClubs = clubs.Count(c => c.IsActive),
                InactiveClubs = clubs.Count(c => !c.IsActive),
                Clubs = clubs
            };

            return Json(result, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public ActionResult ToggleClubStatus(int clubId)
        {
            if (Session["UserEmail"] == null)
                return Json(new { success = false, message = "Not logged in." });

            string userEmail = Session["UserEmail"].ToString().ToLower();

            // Get all sub-department IDs where user is SubHOD or Mentor
            var subHodDeptIds = _db.SUBDEPARTMENTs
                .Where(sd => sd.HOD_Email.ToLower() == userEmail)
                .Select(sd => sd.SubDepartmentID)
                .ToList();

            var mentorDeptIds = _db.Logins
                .Where(l => l.Email.ToLower() == userEmail && l.Role == "Mentor" && l.SubDepartmentID.HasValue)
                .Select(l => l.SubDepartmentID.Value)
                .ToList();

            var allDeptIds = subHodDeptIds.Union(mentorDeptIds).ToList();

            // Find the club under any of these sub-departments
            var club = _db.CLUBS.FirstOrDefault(c => c.ClubID == clubId && c.SubDepartmentID.HasValue && allDeptIds.Contains(c.SubDepartmentID.Value));

            if (club == null)
                return Json(new { success = false, message = "Club not found or not under your accessible sub-departments." });

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

            string userEmail = Session["UserEmail"].ToString().ToLower();

            // Get all sub-department IDs where the user is HOD
            var subDeptIdsAsHOD = _db.SUBDEPARTMENTs
                .Where(sd => sd.HOD_Email.ToLower() == userEmail)
                .Select(sd => sd.SubDepartmentID)
                .ToList();

            // Get all sub-department IDs where the user is a mentor
            var subDeptIdsAsMentor = _db.Logins
                .Where(l => l.Email.ToLower() == userEmail && l.Role == "Mentor")
                .Select(l => l.SubDepartmentID)
                .Where(id => id.HasValue)
                .Select(id => id.Value)
                .ToList();

            // Merge all sub-department IDs (avoid duplicates)
            var allSubDeptIds = subDeptIdsAsHOD
                .Union(subDeptIdsAsMentor)
                .ToList();

            if (!allSubDeptIds.Any())
                return Json(new { error = "No sub-departments found for this user." }, JsonRequestBehavior.AllowGet);

            var clubs = (from c in _db.CLUBS
                         join d in _db.DEPARTMENTs on c.DepartmentID equals d.DepartmentID
                         join sd in _db.SUBDEPARTMENTs on c.SubDepartmentID equals sd.SubDepartmentID
                         where c.SubDepartmentID.HasValue && allSubDeptIds.Contains(c.SubDepartmentID.Value)
                         select new
                         {
                             c.ClubID,
                             c.ClubName,
                             IsActive = c.IsActive ?? false,
                             DepartmentName = d.DepartmentName,
                             SubDepartmentName = sd.SubDepartmentName
                         }).ToList();

            var result = new
            {
                TotalClubs = clubs.Count,
                ActiveClubs = clubs.Count(c => c.IsActive),
                InactiveClubs = clubs.Count(c => !c.IsActive),
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
