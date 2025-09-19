
using SendGrid.Helpers.Mail;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Web.UI.WebControls;
using WebApplication4.Controllers;
using WebApplication4.Models;


namespace WebApplication1.Controllers
{
    public class ClubAdminController : BaseController
    {
        private readonly dummyclubsEntities _db = new dummyclubsEntities(); // Database context // Database context

        public ActionResult Index()
        {
            if (Session["UserEmail"] == null)
            {
                return RedirectToAction("Login", "Admin");
            }

            string userEmail = Session["UserEmail"].ToString();

            var clubAdmin = _db.Logins.FirstOrDefault(l => l.Email == userEmail && l.Role == "ClubAdmin");
            if (clubAdmin == null)
            {
                return HttpNotFound("Club Admin not found");
            }

            int loginId = Convert.ToInt32(Session["UserID"]);
            System.Diagnostics.Debug.WriteLine($"[DEBUG] clubadmin LoginID: {loginId}");

            // ✅ Get club using ClubID directly from the login
            var club = _db.CLUBS.FirstOrDefault(c => c.ClubID == clubAdmin.ClubID && c.IsActive == true);
            if (club == null)
            {
                return HttpNotFound("Associated club not found for this Club Admin.");
            }

            int clubId = club.ClubID;

            // ✅ Notifications
            var notifications = _db.Notifications
                                    .Where(n => n.LoginID == loginId && (n.IsRead ?? false) == false && n.EndDate > DateTime.Now)
                                    .ToList();
            ViewBag.Notifications = notifications;

            // ✅ Event Status Notifications (Updated Section)
            var eventNotifications = _db.EVENTS
                .Where(e => e.ClubID == clubId)
                .Select(e => new
                {
                    e.EventID,
                    e.EventName,
                    e.EventStatus,
                    e.ApprovalStatusID // 👈 Added
                })
                .ToList();

            var eventCards = eventNotifications.Select(ev => new
            {
                Message = ev.EventStatus == "Concluded"
                    ? $"{ev.EventName} - Concluded"
                    : ev.ApprovalStatusID == 2
                        ? $"{ev.EventName} - Approved"
                        : $"{ev.EventName} - Pending Approval",

                Url = ev.EventStatus == "Concluded"
                        ? Url.Action("ConcludedEvents", "ClubAdmin", new { eventId = ev.EventID })
                        : ev.ApprovalStatusID == 1
                            ? Url.Action("UpcomingEvents", "ClubAdmin", new { eventId = ev.EventID })
                            : null // 👉 pending events won’t have a link
            }).ToList();

            ViewBag.EventCards = eventCards;


            // ✅ Info for dashboard
            ViewBag.ClubAdminName = club.ClubName;
            ViewBag.ClubName = club.ClubName;
            ViewBag.ClubAdminPhoto = club.LogoImagePath;
            ViewBag.University = _db.UNIVERSITies
                                    .FirstOrDefault(u => u.UniversityID == clubAdmin.UniversityID)?.UniversityNAME;

            // ✅ Department & SubDepartment Names
            var department = _db.DEPARTMENTs.FirstOrDefault(d => d.DepartmentID == club.DepartmentID);
            var subDepartment = club.SubDepartmentID != null
                ? _db.SUBDEPARTMENTs.FirstOrDefault(s => s.SubDepartmentID == club.SubDepartmentID)
                : null;

            ViewBag.Department = department?.DepartmentName;
            ViewBag.SubDepartment = subDepartment?.SubDepartmentName;

            // ✅ Statistics
            ViewBag.NumberOfEvents = _db.EVENTS.Count(e => e.ClubID == clubId);
            ViewBag.NumberOfClubs = 1;

            return View();
        }


        //
        // ============================================
        // 🔹 New Method: Upload & Save Club Logo
        // ============================================
        [HttpPost]
        public ActionResult UploadClubLogo(HttpPostedFileBase ClubLogo)
        {
            if (Session["UserEmail"] == null)
            {
                return RedirectToAction("Login", "Admin");
            }

            string userEmail = Session["UserEmail"].ToString();
            var clubAdmin = _db.Logins.FirstOrDefault(l => l.Email == userEmail && l.Role == "ClubAdmin");

            if (clubAdmin == null)
            {
                return HttpNotFound("Club Admin not found");
            }

            var club = _db.CLUBS.FirstOrDefault(c => c.ClubID == clubAdmin.ClubID && c.IsActive == true);
            if (club == null)
            {
                return HttpNotFound("Associated club not found.");
            }

            if (ClubLogo != null && ClubLogo.ContentLength > 0)
            {
                string fileName = Path.GetFileName(ClubLogo.FileName);
                string folderPath = Server.MapPath("~/Uploads/ClubLogos/");

                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }

                string filePath = Path.Combine(folderPath, fileName);
                ClubLogo.SaveAs(filePath);

                // Save relative path in DB
                club.LogoImagePath = "/Uploads/ClubLogos/" + fileName;
                _db.SaveChanges();
            }

            return RedirectToAction("Index");
        }





        // GET: Request Event Form
        public ActionResult RequestEvent()
        {
            if (Session["UserEmail"] == null)
            {
                return RedirectToAction("Login", "Admin");
            }

            string userEmail = Session["UserEmail"].ToString();

            // Get login record for club admin
            var loginRecord = _db.Logins.FirstOrDefault(l => l.Email == userEmail && l.Role == "ClubAdmin");
            if (loginRecord == null)
            {
                return HttpNotFound("Club Admin not found");
            }

            // ✅ Fetch club using ClubID from login
            var club = _db.CLUBS.FirstOrDefault(c => c.ClubID == loginRecord.ClubID && c.IsActive == true);
            if (club == null)
            {
                return HttpNotFound("Associated club not found for this Club Admin.");
            }

            var department = _db.DEPARTMENTs.FirstOrDefault(d => d.DepartmentID == club.DepartmentID);
            var university = _db.UNIVERSITies.FirstOrDefault(u => u.UniversityID == loginRecord.UniversityID);

            var model = new EVENT
            {
                ClubID = club.ClubID,
                ClubName = club.ClubName,
                Department = department?.DepartmentName,
                University = university?.UniversityNAME
            };

            ViewBag.OrganizerName = club.ClubName + " Admin";

            return View(model);
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> RequestEvent(
            EVENT model, HttpPostedFileBase EventPoster, HttpPostedFileBase BudgetDocument)
        {
            if (Session["UserEmail"] == null)
                return RedirectToAction("Login", "Admin");

            if (!ModelState.IsValid)
                return View(model);

            if (EventPoster != null && EventPoster.FileName == null)
                throw new Exception("EventPoster.FileName is null");

            if (BudgetDocument != null && BudgetDocument.FileName == null)
                throw new Exception("BudgetDocument.FileName is null");

            if (Server.MapPath("~/uploads") == null)
                throw new Exception("Server.MapPath returned null");


            string userEmail = Session["UserEmail"]?.ToString();
            if (string.IsNullOrEmpty(userEmail))
            {
                TempData["ErrorMessage"] = "User email not found in session.";
                return View(model);
            }

            var loginRecord = _db.Logins.FirstOrDefault(l => l.Email == userEmail && l.Role == "ClubAdmin");
            if (loginRecord == null)
            {
                TempData["ErrorMessage"] = "Club Admin not found.";
                return View(model);
            }

            var club = _db.CLUBS.FirstOrDefault(c => c.ClubID == loginRecord.ClubID && c.IsActive == true);
            if (club == null)
            {
                TempData["ErrorMessage"] = "Associated club not found.";
                return View(model);
            }

            string uploadsFolder = Server.MapPath("~/uploads") ?? Server.MapPath("/");
            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);
            // Event Poster
            string posterPath = "/uploads/default-poster.png";
            if (EventPoster != null && !string.IsNullOrWhiteSpace(EventPoster.FileName) && EventPoster.ContentLength > 0)
            {
                string safeFileName = Path.GetFileName(EventPoster.FileName ?? "default.png"); // safe fallback
                string uniqueFileName = Guid.NewGuid() + "_" + safeFileName;
                string savePath = Path.Combine(uploadsFolder, uniqueFileName);
                EventPoster.SaveAs(savePath);
                posterPath = "/uploads/" + uniqueFileName;
            }

            // Budget Document
            string budgetPath = null;
            if (BudgetDocument != null && !string.IsNullOrWhiteSpace(BudgetDocument.FileName) && BudgetDocument.ContentLength > 0)
            {
                string safeBudgetFileName = Path.GetFileName(BudgetDocument.FileName ?? "default.pdf"); // safe fallback
                string budgetFileName = Guid.NewGuid() + "_" + safeBudgetFileName;
                string budgetSavePath = Path.Combine(uploadsFolder, budgetFileName);
                BudgetDocument.SaveAs(budgetSavePath);
                budgetPath = "/uploads/" + budgetFileName;
            }


            var newEvent = new EVENT
            {
                EventName = model.EventName ?? "Unnamed Event",
                EventDescription = model.EventDescription ?? "No description provided",
                ClubID = club.ClubID,
                EventOrganizerID = loginRecord.LoginID,
                EventType = "Campus",
                ApprovalStatusID = 1,
                EventCreatedDate = DateTime.Now,
                EventStartDateAndTime = model.EventStartDateAndTime,
                EventEndDateAndTime = model.EventEndDateAndTime,
                EventBudget = model.EventBudget,
                BudgetDocumentPath = budgetPath,
                EventPoster = posterPath,
                Venue = model.Venue ?? "Venue not specified",
                IsActive = false
            };

            try
            {
                _db.EVENTS.Add(newEvent);
                _db.SaveChanges();

                var mentorLogin = _db.Logins.FirstOrDefault(m => m.LoginID == club.MentorID);

                Debug.WriteLine($"mentorLogin = {(mentorLogin != null ? "found" : "null")}");

                if (mentorLogin != null && !string.IsNullOrEmpty(mentorLogin.Email))
                {
                    string mentorEmail = mentorLogin.Email;
                    Debug.WriteLine($"mentorEmail = '{mentorEmail}'");

                    var mentorUser = _db.USERs.FirstOrDefault(u => u.Email == mentorEmail);
                    Debug.WriteLine($"mentorUser = {(mentorUser != null ? "found" : "null")}");
                    Debug.WriteLine($"mentorUser.FirstName = '{mentorUser?.FirstName}'");
                    Debug.WriteLine($"mentorUser.LastName = '{mentorUser?.LastName}'");

                    string mentorFullName = mentorUser != null
                        ? $"{mentorUser.FirstName ?? ""} {mentorUser.LastName ?? ""}".Trim()
                        : "Mentor";
                    Debug.WriteLine($"mentorFullName = '{mentorFullName}'");

                    // Token generation
                    // Token generation (with source = email)
                    string plainData = $"{newEvent.EventID}|{club.ClubID}|{mentorLogin.LoginID}";
                    string encryptedToken = SecureHelper.Encrypt(plainData);

                    string token = !string.IsNullOrEmpty(encryptedToken) ? encryptedToken : "defaultToken";



                    // Base URL (use PC's local IP for mobile testing)
                    string baseUrl = "https://localhost:44368"; // Use http for local testing
                    Debug.WriteLine($"baseUrl = '{baseUrl}'");

                    // Forward & Reject URLs (absolute URLs)
                    string forwardUrl = Url.Action("ForwardEventToHOD", "Mentor", new { token = token }, Request.Url.Scheme);
                    string rejectUrl = Url.Action("RejectEventRequest", "Mentor", new { token = token }, Request.Url.Scheme);
                    Debug.WriteLine($"forwardUrl = '{forwardUrl}'");
                    Debug.WriteLine($"rejectUrl = '{rejectUrl}'");

                    // Poster & Budget Document URLs (absolute URLs)
                    string posterUrl = !string.IsNullOrEmpty(newEvent.EventPoster)
                        ? $"{baseUrl}{newEvent.EventPoster}"
                        : $"{baseUrl}/uploads/default-poster.png";

                    string budgetDocUrl = !string.IsNullOrEmpty(newEvent.BudgetDocumentPath)
                        ? $"{baseUrl}{newEvent.BudgetDocumentPath}"
                        : "#";

                    Debug.WriteLine($"posterUrl = '{posterUrl}'");
                    Debug.WriteLine($"budgetDocUrl = '{budgetDocUrl}'");

                    // Club name safe
                    string clubNameSafe = club.ClubName ?? "Unknown Club";
                    Debug.WriteLine($"clubNameSafe = '{clubNameSafe}'");


                    // Email body
                    string emailBody = $@"
<div style='font-family: Arial, sans-serif; font-size: 14px; color: #000; line-height: 1.5;'>
    <h3 style='margin-bottom: 10px;'>Dear {mentorFullName},</h3>
    <div style='margin-bottom: 10px;'>
        A new event request has been submitted by <strong>{clubNameSafe}</strong>.
    </div>
    <div style='margin-bottom: 10px;'><strong>Event:</strong> {newEvent.EventName}</div>
    <div style='margin-bottom: 10px;'><strong>Description:</strong> {newEvent.EventDescription}</div>
    <div style='margin-bottom: 10px;'><strong>Dates:</strong> {newEvent.EventStartDateAndTime} - {newEvent.EventEndDateAndTime}</div>
    <div style='margin-bottom: 10px;'><strong>Venue:</strong> {newEvent.Venue}</div>
    <div style='margin-bottom: 10px;'><strong>Budget:</strong> {newEvent.EventBudget}</div>
    <div style='margin-bottom: 10px;'>
        <strong>Budget Document:</strong> <a href='{budgetDocUrl}' style='color: #1a73e8;'>View Document</a>
    </div>

    <div style='margin-top:15px;'>
        <a href='{forwardUrl}' style='padding:10px 15px; background-color:#28a745; color:#fff; text-decoration:none; margin-right:10px; display:inline-block;'>Forward to SCC</a>
        <a href='{rejectUrl}' style='padding:10px 15px; background-color:#dc3545; color:#fff; text-decoration:none; display:inline-block;'>Reject Event</a>
    </div>
    <!-- Invisible spacer to prevent Gmail collapse -->
    <div style='display:none; font-size:1px; line-height:1px;'>&nbsp;</div>
</div>";


                    try
                    {
                        var emailService = new EmailService();
                        await emailService.SendEmailAsync(mentorEmail, "Event Request for Approval", emailBody);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Email sending failed: {ex.Message}");
                        TempData["WarningMessage"] = "Event saved, but email could not be sent to mentor.";
                    }
                }
                else
                {
                    TempData["WarningMessage"] = "Event saved, but mentor not found. Cannot send email.";
                }


                TempData["SuccessMessage"] = "Event request submitted successfully!";
                return RedirectToAction("RequestEvent");
            }
            catch (Exception ex)
            {
                while (ex.InnerException != null)
                    ex = ex.InnerException;

                TempData["ErrorMessage"] = "Error: " + ex.Message;
                return View(model);
            }
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

        public ActionResult UpcomingEvents()
        {
            // Check if the user is logged in
            if (Session["UserEmail"] == null)
            {
                return RedirectToAction("Login", "Admin");
            }

            string userEmail = Session["UserEmail"].ToString();

            // Fetch Club Admin details
            var clubAdmin = _db.Logins.FirstOrDefault(c => c.Email == userEmail);

            if (clubAdmin == null)
                return HttpNotFound("Club Admin not found");

            int clubId = clubAdmin.ClubID ?? 0;

            using (var db = new dummyclubsEntities())
            {
                DateTime now = DateTime.Now;

                // Approved but not yet posted, only upcoming events
                var approvedNotPosted = db.EVENTS
                    .Where(e => e.ApprovalStatusID == 2
                                && e.IsActive == false
                                && e.ClubID == clubId
                                && e.EventEndDateAndTime >= now)
                    .ToList();

                // Approved and already posted upcoming events (not yet concluded)
                var postedUpcoming = db.EVENTS
                    .Where(e => e.ApprovalStatusID == 2
                                && e.IsActive == true
                                && e.EventStatus == "Upcoming posted"
                                && e.ClubID == clubId
                                && e.EventEndDateAndTime >= now)
                    .ToList();

                var model = new UpcomingEventsViewModel
                {
                    ApprovedNotPostedEvents = approvedNotPosted,
                    PostedUpcomingEvents = postedUpcoming
                };

                return View(model);
            }
        }



        public ActionResult PostEvent(int eventId)
        {
            var ev = _db.EVENTS
                        .FirstOrDefault(e => e.EventID == eventId && e.EventEndDateAndTime >= DateTime.Now);

            if (ev == null)
                return HttpNotFound("Event not found or already ended.");

            // Fetch ClubName from ClubID
            string clubName = string.Empty;
            if (ev.ClubID != null)
            {
                var club = _db.CLUBS.FirstOrDefault(c => c.ClubID == ev.ClubID);
                if (club != null)
                    clubName = club.ClubName;
            }

            // Fetch Organizer Name from LOGIN -> ClubRegistrations
            string organizerName = string.Empty;
            var login = _db.Logins.FirstOrDefault(l => l.LoginID == ev.EventOrganizerID);
            if (login != null)
            {
                var clubReg = _db.ClubRegistrations.FirstOrDefault(cr => cr.Email == login.Email);
                if (clubReg != null)
                    organizerName = clubReg.FullName;
            }

            var model = new PostEventViewModel
            {
                EventID = ev.EventID,
                EventName = ev.EventName,
                EventDescription = ev.EventDescription,
                EventStartDateAndTime = ev.EventStartDateAndTime,
                EventEndDateAndTime = ev.EventEndDateAndTime,
                RegistrationURL = ev.RegistrationURL,
                QRContentText = ev.QRContentText,
                OrganizerName = organizerName,
                ClubName = clubName,
                Venue = ev.Venue,
                EventPoster = ev.EventPoster,       // read-only
                EventBanner = ev.EventBannerPath    // editable
            };

            return View(model);
        }


        [HttpPost]
        public ActionResult PostEvent(PostEventViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            try
            {
                var ev = _db.EVENTS.Find(model.EventID);
                if (ev != null)
                {
                    // Only update editable fields
                    ev.RegistrationURL = model.RegistrationURL;

                    // If you store organizer name in EVENTS table, uncomment:
                    // ev.OrganizerName = model.OrganizerName;

                    if (model.EventBannerFile != null && model.EventBannerFile.ContentLength > 0)
                    {
                        var bannerDirectory = Server.MapPath("~/UploadedBanners");
                        if (!Directory.Exists(bannerDirectory))
                            Directory.CreateDirectory(bannerDirectory);

                        var bannerFileName = Path.GetFileName(model.EventBannerFile.FileName);
                        var bannerPath = Path.Combine(bannerDirectory, bannerFileName);
                        model.EventBannerFile.SaveAs(bannerPath);
                        ev.EventBannerPath = "/UploadedBanners/" + bannerFileName;
                    }

                    // Mark event as posted
                    ev.EventStatus = "Upcoming posted";
                    ev.IsActive = true;

                    _db.SaveChanges();
                }

                ViewBag.Message = "Event posted successfully!";
                return View("PostEvent", model);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "An unexpected error occurred. Please try again.");
                System.Diagnostics.Debug.WriteLine(ex.Message);
                return View(model);
            }
        }



        private string DetermineEventStatus(DateTime start, DateTime end)
        {
            if (DateTime.Now < start)
                return "Upcoming";
            else if (DateTime.Now >= start && DateTime.Now <= end)
                return "Ongoing";
            else
                return "Completed";
        }

        public ActionResult EditEvent(int eventId)
        {
            var ev = _db.EVENTS.Find(eventId);

            if (ev == null)
            {
                return HttpNotFound("Event not found.");
            }

            // Step 1: Get ClubName from ClubID
            string clubName = string.Empty;
            if (ev.ClubID != null)
            {
                var club = _db.CLUBS.FirstOrDefault(c => c.ClubID == ev.ClubID);
                if (club != null)
                {
                    clubName = club.ClubName;
                }
            }

            // Step 2: Get Organizer Email from LOGIN table using OrganizerID
            string organizerName = string.Empty;
            var login = _db.Logins.FirstOrDefault(l => l.LoginID == ev.EventOrganizerID);
            if (login != null)
            {
                // Step 3: Get Organizer Name from CLUBREGISTRATIONS using Email
                var clubReg = _db.ClubRegistrations.FirstOrDefault(cr => cr.Email == login.Email);
                if (clubReg != null)
                {
                    organizerName = clubReg.FullName;
                }
            }

            var model = new PostEventViewModel
            {
                EventID = ev.EventID,
                EventName = ev.EventName,
                EventDescription = ev.EventDescription,
                EventStartDateAndTime = ev.EventStartDateAndTime,
                EventEndDateAndTime = ev.EventEndDateAndTime,
                RegistrationURL = ev.RegistrationURL,
                QRContentText = ev.QRContentText,
                OrganizerName = organizerName,
                ClubName = clubName,
                EventPoster=ev.EventPoster,
                EventBanner=ev.EventBannerPath,
                Venue="ICFAI Foundation,Hydreabad",
               // EventBanner = ev.EventBannerPath
            };

            return View(model);
        }



        [HttpPost]
        public ActionResult EditEvent(PostEventViewModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    // Ensure the UploadedPosters directory exists
                    var posterDirectory = Server.MapPath("~/UploadedPosters");
                    if (!Directory.Exists(posterDirectory))
                    {
                        Directory.CreateDirectory(posterDirectory);
                    }


                    // Save uploaded poster
                    if (model.EventPosterFile != null && model.EventPosterFile.ContentLength > 0)
                    {
                        var posterFileName = Path.GetFileName(model.EventPosterFile.FileName);
                        var posterPath = Path.Combine(posterDirectory, posterFileName);
                        model.EventPosterFile.SaveAs(posterPath);
                        model.EventPoster = "/UploadedPosters/" + posterFileName;
                    }

                    // Ensure the UploadedBanners directory exists
                    var bannerDirectory = Server.MapPath("~/UploadedBanners");
                    if (!Directory.Exists(bannerDirectory))
                    {
                        Directory.CreateDirectory(bannerDirectory);
                    }

                    // Save uploaded banner
                    if (model.EventBannerFile != null && model.EventBannerFile.ContentLength > 0)
                    {
                        var bannerFileName = Path.GetFileName(model.EventBannerFile.FileName);
                        var bannerPath = Path.Combine(bannerDirectory, bannerFileName);
                        model.EventBannerFile.SaveAs(bannerPath);
                        model.EventBanner = "/UploadedBanners/" + bannerFileName;
                    }

                    // Update event in DB
                    var ev = _db.EVENTS.Find(model.EventID);
                    if (ev != null)
                    {
                        ev.EventDescription = model.EventDescription;
                        ev.EventStartDateAndTime = model.EventStartDateAndTime;
                        ev.EventEndDateAndTime = model.EventEndDateAndTime;
                        ev.EventPoster = model.EventPoster;
                        ev.EventBannerPath = model.EventBanner;

                        if (!string.IsNullOrWhiteSpace(model.RegistrationURL))
                        {
                            ev.RegistrationURL = model.RegistrationURL;
                        }

                        // ev.EventStatus = "Upcoming not posted";

                        // Debug output
                        System.Diagnostics.Debug.WriteLine("===== Event Details Before Save =====");
                        System.Diagnostics.Debug.WriteLine("EventID: " + ev.EventID);
                        System.Diagnostics.Debug.WriteLine("EventDescription: " + ev.EventDescription);
                        System.Diagnostics.Debug.WriteLine("EventStartDateAndTime: " + ev.EventStartDateAndTime);
                        System.Diagnostics.Debug.WriteLine("EventEndDateAndTime: " + ev.EventEndDateAndTime);
                        System.Diagnostics.Debug.WriteLine("EventPoster: " + ev.EventPoster);
                        System.Diagnostics.Debug.WriteLine("EventBannerPath: " + ev.EventBannerPath);
                        System.Diagnostics.Debug.WriteLine("EventStatus: " + ev.EventStatus);
                        System.Diagnostics.Debug.WriteLine("=====================================");

                        _db.SaveChanges();
                    }

                    
                    ViewBag.Message = "Event updated successfully!";
                    return View(new PostEventViewModel());
                }
                catch (System.Data.Entity.Validation.DbEntityValidationException ex)
                {
                    foreach (var entityValidationErrors in ex.EntityValidationErrors)
                    {
                        foreach (var validationError in entityValidationErrors.ValidationErrors)
                        {
                            ModelState.AddModelError(validationError.PropertyName, validationError.ErrorMessage);
                            // Optional: Log to console or file for debugging
                            System.Diagnostics.Debug.WriteLine($"Property: {validationError.PropertyName} Error: {validationError.ErrorMessage}");
                        }
                    }
                }

                catch (Exception ex)
                {
                    // Log the exception details
                    ModelState.AddModelError("", "An unexpected error occurred. Please try again.");
                }
            }

            // If we reach here, something went wrong; redisplay the form
            return View(model);
        }


        // GET: Concluded Events
        public ActionResult ConcludedEvents()
        {
            if (Session["UserEmail"] == null)
                return RedirectToAction("Login", "Admin");

            string userEmail = Session["UserEmail"].ToString();
            var clubAdmin = _db.Logins.FirstOrDefault(c => c.Email == userEmail);
            if (clubAdmin == null) return HttpNotFound("Club Admin not found");

            int clubId = clubAdmin.ClubID ?? 0;
            var today = DateTime.Today;

            // Update events that ended
            var eventsToUpdate = _db.EVENTS
                .Where(e => e.EventEndDateAndTime < today && e.EventStatus != "Concluded" && e.ClubID == clubId)
                .ToList();

            foreach (var ev in eventsToUpdate)
                ev.EventStatus = "Concluded";

            _db.SaveChanges();

            // Return all concluded events
            var concludedEvents = _db.EVENTS
                .Where(e => e.EventStatus == "Concluded" && e.ClubID == clubId)
                .ToList();

            return View(concludedEvents);
        }

        // GET: Concluded Event Details
        public ActionResult getconEventDetails(int id)
        {
            var eventDetails = _db.EVENTS.Find(id);
            if (eventDetails == null) return HttpNotFound();

            var eventPhotos = _db.EventPhotos.Where(e => e.EventId == id).ToList();
            var eventWinners = _db.EventWinners.Where(w => w.EventId == id).ToList();

            var model = new EventDetailsViewModel
            {
                Event = eventDetails,
                EventPhotos = eventPhotos,
                EventWinners = eventWinners
            };

            return View(model);
        }

        // GET: Upload Event Details
        [HttpGet]
        public ActionResult SaveEventDetails(int id)
        {
            var model = new save_event_detailsview { EventId = id };
            return View(model);
        }

        // POST: Save Event Details → Pending Mentor Approval
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SaveEventDetails(
            int EventId,
            HttpPostedFileBase Brochure,
            IEnumerable<HttpPostedFileBase> EventPhotos,
            IEnumerable<HttpPostedFileBase> EventVideos,
            List<EventWinner> Winners,
            string DeletedPhotoIds,
            string DeletedWinnerIds
        )
        {
            var evnt = _db.EVENTS.Find(EventId);
            if (evnt == null)
            {
                ViewBag.ErrorMessage = "Event not found.";
                return View("EventDetails");
            }

            // --- Brochure Upload ---
            if (Brochure != null && Brochure.ContentLength > 0)
            {
                string brochureDir = Server.MapPath("~/wwwroot/UploadedBrochures/");
                if (!Directory.Exists(brochureDir)) Directory.CreateDirectory(brochureDir);

                if (!string.IsNullOrEmpty(evnt.EventBrochure))
                {
                    var oldBrochurePath = Server.MapPath(evnt.EventBrochure);
                    if (System.IO.File.Exists(oldBrochurePath)) System.IO.File.Delete(oldBrochurePath);
                }

                string brochureFileName = Path.GetFileName(Brochure.FileName);
                string brochurePath = Path.Combine(brochureDir, brochureFileName);
                Brochure.SaveAs(brochurePath);

                evnt.EventBrochure = "/wwwroot/UploadedBrochures/" + brochureFileName;

                // Set brochure approval status for mentor
                evnt.BrochureApprovalStatusID = 1; // Pending
                evnt.BrochureApprovedByID = null;
                evnt.BrochureApprovedDate = null;
            }

            // --- Photos Upload ---
            if (EventPhotos != null)
            {
                string photoDir = Server.MapPath("~/wwwroot/Images/");
                if (!Directory.Exists(photoDir)) Directory.CreateDirectory(photoDir);

                foreach (var photo in EventPhotos)
                {
                    if (photo != null && photo.ContentLength > 0)
                    {
                        string photoFileName = Path.GetFileName(photo.FileName);
                        string photoPath = Path.Combine(photoDir, photoFileName);
                        photo.SaveAs(photoPath);

                        var eventPhoto = new EventPhoto
                        {
                            EventId = EventId,
                            Path = "/wwwroot/Images/" + photoFileName,
                            UploadedDate = DateTime.Now,
                            MediaType = "Photo",
                            ApprovalStatusID = 1, // Pending
                            ApprovedByID = null,
                            ApprovedDate = null
                        };
                        _db.EventPhotos.Add(eventPhoto);
                    }
                }
            }

            // --- Videos Upload ---
            if (EventVideos != null)
            {
                string videoDir = Server.MapPath("~/wwwroot/Videos/");
                if (!Directory.Exists(videoDir)) Directory.CreateDirectory(videoDir);

                foreach (var video in EventVideos)
                {
                    if (video != null && video.ContentLength > 0)
                    {
                        string videoFileName = Path.GetFileName(video.FileName);
                        string videoPath = Path.Combine(videoDir, videoFileName);
                        video.SaveAs(videoPath);

                        var eventVideo = new EventPhoto
                        {
                            EventId = EventId,
                            Path = "/wwwroot/Videos/" + videoFileName,
                            UploadedDate = DateTime.Now,
                            MediaType = "Video",
                            ApprovalStatusID = 1, // Pending
                            ApprovedByID = null,
                            ApprovedDate = null
                        };
                        _db.EventPhotos.Add(eventVideo);
                    }
                }
            }

            // --- Delete Photos if needed ---
            if (!string.IsNullOrEmpty(DeletedPhotoIds))
            {
                var ids = DeletedPhotoIds.Split(',').Select(int.Parse).ToList();
                var photosToDelete = _db.EventPhotos.Where(p => ids.Contains(p.Id)).ToList();
                _db.EventPhotos.RemoveRange(photosToDelete);
            }

            // --- Winners Upload (Pending Mentor Approval) ---
            if (Winners != null)
            {
                foreach (var winner in Winners)
                {
                    if (!string.IsNullOrEmpty(winner.WinnerName))
                    {
                        winner.EventId = EventId;
                        winner.ApprovalStatusID = 1;  // Pending
                        winner.ApprovedByID = null;
                        winner.ApprovedDate = null;
                        _db.EventWinners.Add(winner);
                    }
                }
            }

            // --- Delete Winners if needed ---
            if (!string.IsNullOrEmpty(DeletedWinnerIds))
            {
                var ids = DeletedWinnerIds.Split(',').Select(int.Parse).ToList();
                var winnersToDelete = _db.EventWinners.Where(w => ids.Contains(w.Id)).ToList();
                _db.EventWinners.RemoveRange(winnersToDelete);
            }

            _db.SaveChanges();

            ViewBag.SuccessMessage = "Event details uploaded successfully! Pending mentor approval.";

            var model = new EventDetailsViewModel
            {
                Event = evnt,
                EventPhotos = _db.EventPhotos.Where(p => p.EventId == EventId).ToList(),
                EventWinners = _db.EventWinners.Where(w => w.EventId == EventId).ToList()
            };

            return View("getconEventDetails", model);
        }



        //change password
        [HttpGet]
        public ActionResult ChangePassword()
        {
            if (Session["UserEmail"] == null)
            {
                TempData["ErrorMessage"] = "Your session has expired. Please login again.";
                return RedirectToAction("Login", "Admin"); // ✅ Redirects to AdminController
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
                return RedirectToAction("Login", "Admin"); // ✅ Redirects to AdminController
            }

            var user = _db.Logins.FirstOrDefault(u => u.Email == userEmail);

            if (user == null)
            {
                ModelState.AddModelError("", "User not found.");
                return View(model);
            }

            // WARNING: This assumes plain text password comparison — not secure in production!
            if (user.PasswordHash != model.CurrentPassword)
            {
                ModelState.AddModelError("", "Current password is incorrect.");
                return View(model);
            }

            // Update password
            user.PasswordHash = model.NewPassword;
            _db.SaveChanges();

            TempData["SuccessMessage"] = "Password changed successfully!";
            return RedirectToAction("ChangePassword", "ClubAdmin");
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
