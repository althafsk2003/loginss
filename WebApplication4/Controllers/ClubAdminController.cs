﻿
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
using WebApplication4.Models;


namespace WebApplication1.Controllers
{
    public class ClubAdminController : Controller
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

            // ✅ Info for dashboard
            ViewBag.ClubAdminName = club.ClubName;
            ViewBag.ClubName = club.ClubName;
            ViewBag.ClubAdminPhoto = club.LogoImagePath; // used in view to display logo
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
                    string plainData = $"{newEvent.EventID}|{club.ClubID}|email";
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

            // Get logged-in user's email
            string userEmail = Session["UserEmail"].ToString();

            // Fetch Club Admin details
            var clubAdmin = _db.Logins.FirstOrDefault(c => c.Email == userEmail);

            if (clubAdmin == null)
            {
                return HttpNotFound("Club Admin not found");
            }

            // Get the ClubID for the logged-in club admin
            int clubId = clubAdmin.ClubID ?? 0; // Handle null case if necessary

            using (var db = new dummyclubsEntities())
            {
                // Approved but not yet posted to website, filtered by ClubID
                var approvedNotPosted = db.EVENTS
                    .Where(e => e.ApprovalStatusID == 2 && e.IsActive == false && e.ClubID == clubId)
                    .ToList();

                // Approved and already posted upcoming events (not yet concluded), filtered by ClubID
                var postedUpcoming = db.EVENTS
                    .Where(e => e.ApprovalStatusID == 2 && e.IsActive == true && e.EventStatus == "Upcoming posted" && e.ClubID == clubId)
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
            var ev = _db.EVENTS.Find(eventId);

            if (ev == null)
            {
                return HttpNotFound("Event not found.");
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
                //OrganizerName = ev.OrganizerName,
                ClubName = ev.ClubName,
                EventBanner = ev.EventBannerPath
            };

            return View(model);
        }

      
        [HttpPost]
        public ActionResult PostEvent(PostEventViewModel model)
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
                        ev.EventStatus = "Upcoming not posted";

                        if (!string.IsNullOrWhiteSpace(model.RegistrationURL))
                        {
                            ev.RegistrationURL = model.RegistrationURL;
                        }


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


                    ViewBag.Message = "Event posted successfully!";
                    return View("PostEvent", model);
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


        public ActionResult ConcludedEvents()
        {
            // Check if the user is logged in
            if (Session["UserEmail"] == null)
            {
                return RedirectToAction("Login", "Admin");
            }

            // Get logged-in user's email
            string userEmail = Session["UserEmail"].ToString();

            // Fetch Club Admin details
            var clubAdmin = _db.Logins.FirstOrDefault(c => c.Email == userEmail);

            if (clubAdmin == null)
            {
                return HttpNotFound("Club Admin not found");
            }

            // Get the ClubID for the logged-in club admin
            int clubId = clubAdmin.ClubID ?? 0; // Handle null case if necessary

            var today = DateTime.Today;

            // Step 1: Find events where end date < today but status is not yet 'Concluded', filtered by ClubID
            var eventsToUpdate = _db.EVENTS
                .Where(e => e.EventEndDateAndTime < today && e.EventStatus != "Concluded" && e.ClubID == clubId)
                .ToList();

            // Step 2: Update their status
            foreach (var ev in eventsToUpdate)
            {
                ev.EventStatus = "Concluded";
            }

            // Save changes to database
            _db.SaveChanges();

            // Step 3: Fetch all concluded events for the club admin's club
            var concludedEvents = _db.EVENTS
                .Where(e => e.EventStatus == "Concluded" && e.ClubID == clubId)
                .ToList();

            // Send to view
            return View(concludedEvents);
        }

        // GET: Event/Details/5 (Display Event Details and Form to Add Photos & Winners)
        public ActionResult getconEventDetails(int id)
        {
            var eventDetails = _db.EVENTS.Find(id);
            if (eventDetails == null)
            {
                return HttpNotFound();
            }

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

        /*[HttpPost]
        [ValidateAntiForgeryToken]  // Ensure the anti-forgery token is validated
        public ActionResult SaveEventDetails(EventDetailsViewModel model, HttpPostedFileBase Brochure, IEnumerable<HttpPostedFileBase> EventPhotos)
        {
            // Retrieve event details based on the EventID
            var eventDetails = _db.EVENTS.FirstOrDefault(e => e.EventID == model.Event.EventID);
            if (eventDetails == null)
            {
                // Handle case where event details could not be found (optional)
                return HttpNotFound("Event not found");
            }

            // Set up the paths for brochures and photos inside wwwroot (without 'images' folder)
            string brochureDirectory = Server.MapPath("~/wwwroot/UploadedBrochures/");
            string photoDirectory = Server.MapPath("~/wwwroot/Images/");

            // Create directories if they do not exist
            if (!Directory.Exists(brochureDirectory))
            {
                Directory.CreateDirectory(brochureDirectory);
            }

            if (!Directory.Exists(photoDirectory))
            {
                Directory.CreateDirectory(photoDirectory);
            }

            // Save Brochure if provided
            if (Brochure != null && Brochure.ContentLength > 0)
            {
                var brochurePath = Path.Combine(brochureDirectory, Path.GetFileName(Brochure.FileName));
                Brochure.SaveAs(brochurePath);
                eventDetails.EventBrochure = "/UploadedBrochures/" + Path.GetFileName(Brochure.FileName);
            }

            // Update Winners: Remove old winners, then add new ones
            var existingWinners = _db.EventWinners.Where(w => w.EventId == eventDetails.EventID).ToList();
            foreach (var winner in existingWinners)
            {
                _db.EventWinners.Remove(winner);
            }

            foreach (var winner in model.EventWinners)
            {
                winner.EventId = eventDetails.EventID; // Set EventId for each winner
                _db.EventWinners.Add(winner);
            }

            // Save Photos: Remove old photos, then add new ones
            var existingPhotos = _db.EventPhotos.Where(p => p.EventId == eventDetails.EventID).ToList();
            foreach (var photo in existingPhotos)
            {
                _db.EventPhotos.Remove(photo);
            }

            foreach (var photo in EventPhotos)
            {
                if (photo != null && photo.ContentLength > 0)
                {
                    var photoPath = Path.Combine(photoDirectory, Path.GetFileName(photo.FileName));
                    photo.SaveAs(photoPath);

                    // Save photo info in database
                    var eventPhoto = new EventPhoto
                    {
                        EventId = eventDetails.EventID,
                        PhotoPath = "/Images/" + Path.GetFileName(photo.FileName),
                        UploadedDate = DateTime.Now
                    };
                    _db.EventPhotos.Add(eventPhoto);
                }
            }

            // Save changes to the database
            _db.SaveChanges();

            // Redirect back to the event details page
            return RedirectToAction("EventDetails", new { eventId = eventDetails.EventID });
        }*/

        [HttpGet]
        public ActionResult SaveEventDetails(int id)
        {
            var model = new save_event_detailsview
            {
                EventId = id
            };
            return View(model);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SaveEventDetails(
     int EventId,
     HttpPostedFileBase Brochure,
     IEnumerable<HttpPostedFileBase> EventPhotos,
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

            // Handle Brochure Upload
            if (Brochure != null && Brochure.ContentLength > 0)
            {
                string brochureDirectory = Server.MapPath("~/wwwroot/UploadedBrochures/");
                if (!Directory.Exists(brochureDirectory))
                    Directory.CreateDirectory(brochureDirectory);

                if (!string.IsNullOrEmpty(evnt.EventBrochure))
                {
                    var oldBrochurePath = Server.MapPath(evnt.EventBrochure);
                    if (System.IO.File.Exists(oldBrochurePath))
                    {
                        System.IO.File.Delete(oldBrochurePath);
                    }
                }

                string brochureFileName = Path.GetFileName(Brochure.FileName);
                string brochurePath = Path.Combine(brochureDirectory, brochureFileName);
                Brochure.SaveAs(brochurePath);
                evnt.EventBrochure = "/wwwroot/UploadedBrochures/" + brochureFileName;
            }

            // Handle New Photos
            if (EventPhotos != null)
            {
                foreach (var photo in EventPhotos)
                {
                    if (photo != null && photo.ContentLength > 0)
                    {
                        string photoDirectory = Server.MapPath("~/wwwroot/Images/");
                        if (!Directory.Exists(photoDirectory))
                            Directory.CreateDirectory(photoDirectory);

                        string photoFileName = Path.GetFileName(photo.FileName);
                        string photoPath = Path.Combine(photoDirectory, photoFileName);
                        photo.SaveAs(photoPath);

                        var eventPhoto = new EventPhoto
                        {
                            EventId = EventId,
                            PhotoPath = "/wwwroot/Images/" + photoFileName,
                            UploadedDate = DateTime.Now
                        };
                        _db.EventPhotos.Add(eventPhoto);
                    }
                }
            }

            // Handle Deleted Photos
            if (!string.IsNullOrEmpty(DeletedPhotoIds))
            {
                var ids = DeletedPhotoIds.Split(',').Select(int.Parse).ToList();
                var photosToDelete = _db.EventPhotos.Where(p => ids.Contains(p.Id)).ToList();
                _db.EventPhotos.RemoveRange(photosToDelete);
            }

            // Handle New Winners
            if (Winners != null)
            {
                foreach (var winner in Winners)
                {
                    if (!string.IsNullOrEmpty(winner.WinnerName))
                    {
                        winner.EventId = EventId;
                        _db.EventWinners.Add(winner);
                    }
                }
            }

            // Handle Deleted Winners
            if (!string.IsNullOrEmpty(DeletedWinnerIds))
            {
                var ids = DeletedWinnerIds.Split(',').Select(int.Parse).ToList();
                var winnersToDelete = _db.EventWinners.Where(w => ids.Contains(w.Id)).ToList();
                _db.EventWinners.RemoveRange(winnersToDelete);
            }

            _db.SaveChanges();

            // Set success message in ViewBag
            ViewBag.SuccessMessage = "Event details updated successfully!";

            // Rebuild view model for the same view
            var model = new EventDetailsViewModel
            {
                Event = evnt,
                EventPhotos = _db.EventPhotos.Where(p => p.EventId == EventId).ToList(),
                EventWinners = _db.EventWinners.Where(w => w.EventId == EventId).ToList()
            };

            return View("getconEventDetails", model); // Stay on same view
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
