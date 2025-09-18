using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using Newtonsoft.Json;
using QRCoder;
using WebApplication4.Models;
using System.Data.Entity.Validation;
using Org.BouncyCastle.Asn1.Ocsp;
using SendGrid;
using System.Data.Entity.Infrastructure;

namespace WebApplication4.Controllers
{
    public class ClubsController : Controller
    {
        private readonly dummyclubsEntities db = new dummyclubsEntities();
        private readonly EmailService _emailService = new EmailService();  // Injecting EmailService

        // GET: Clubs
        public ActionResult Index()
        {
            return View();
        }

        public ActionResult ClubsandImages()
        {
            return View();
        }

        public ActionResult Departments()
        {
            // Fetch IFHE university ID
            var ifheUniversity = db.UNIVERSITies.FirstOrDefault(u => u.Abbreviation == "IFHE");

            if (ifheUniversity == null)
            {
                ViewBag.ErrorMessage = "IFHE University not found.";
                return View(new List<DEPARTMENT>()); // Return an empty list
            }

            int ifheUniversityId = ifheUniversity.UniversityID;

            // Fetch departments for IFHE university
            List<DEPARTMENT> departments = db.DEPARTMENTs
                .Where(d => d.Universityid == ifheUniversityId && d.IsActive == true)
                .ToList();

            return View(departments); // Directly return the list
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }

        public JsonResult GetClubLogos(int departmentId)
        {
            var clubs = db.CLUBS
                .Where(c => c.DepartmentID == departmentId)
                .Select(c => new { c.ClubID, c.ClubName, c.LogoImagePath })
                .ToList();

            return Json(clubs, JsonRequestBehavior.AllowGet);
        }

        public ActionResult GetClubDetails(int clubId)
        {
            var club = db.CLUBS.Find(clubId);
            return Json(club);
        }

        public ActionResult ClubDetails(int id)
        {
            var club = db.CLUBS.FirstOrDefault(c => c.ClubID == id);
            if (club == null)
            {
                return HttpNotFound();
            }

            var department = db.DEPARTMENTs.FirstOrDefault(d => d.DepartmentID == club.DepartmentID);
            var mentorLogin = db.Logins.FirstOrDefault(m => m.LoginID == club.MentorID);
            var mentorUser = db.USERs.FirstOrDefault(u => u.Email == mentorLogin.Email);

            ViewBag.DepartmentName = department != null ? department.DepartmentName : "Unknown";
            ViewBag.MentorName = mentorUser != null
                ? $"{mentorUser.FirstName} {mentorUser.LastName}"
                : "Unknown";
            ViewBag.MentorEmail = mentorLogin != null ? mentorLogin.Email : "Unknown";

            var university = department != null
                ? db.UNIVERSITies.FirstOrDefault(u => u.UniversityID == department.Universityid)
                : null;

            var departments = university != null
                ? db.DEPARTMENTs
                    .Where(d => d.Universityid == university.UniversityID)
                    .Select(d => new DepartmentDto { Id = d.DepartmentID, Name = d.DepartmentName })
                    .ToList()
                : new List<DepartmentDto>();

            ViewBag.UniversityName = university?.UniversityNAME ?? "Unknown";
            ViewBag.UniversityId = university?.UniversityID;
            ViewBag.Departments = departments;

            return View(club);
        }


        [HttpGet]
        public JsonResult GetUniversityAndDepartments(int clubId)
        {
            var club = db.CLUBS.FirstOrDefault(c => c.ClubID == clubId);
            if (club == null)
            {
                return Json(new { UniversityName = "Unknown", Departments = new List<object>() }, JsonRequestBehavior.AllowGet);
            }

            var department = db.DEPARTMENTs.FirstOrDefault(d => d.DepartmentID == club.DepartmentID);
            var university = db.UNIVERSITies.FirstOrDefault(u => u.UniversityID == department.Universityid);

            var departments = db.DEPARTMENTs
                .Where(d => d.Universityid == university.UniversityID)
                .Select(d => new { Id = d.DepartmentID, Name = d.DepartmentName })
                .ToList();

            return Json(new { UniversityName = university.UniversityNAME, Departments = departments }, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public ActionResult Register(ClubRegistration model, HttpPostedFileBase ProfileImage)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors)
                                              .Select(e => e.ErrorMessage)
                                              .ToList();
                return Json(new { success = false, message = "Invalid data!", errors });
            }

            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    var existingUser = db.USERs.FirstOrDefault(u => u.Email == model.Email);
                    if (existingUser == null)
                    {
                        existingUser = new USER
                        {
                            FirstName = model.FullName,
                            LastName = "null",
                            Email = model.Email,
                            Password = "hashedPassword",
                            SubscriptionStatus = "normal",
                            RegistrationDate = DateTime.Now,
                            UserType = "campus",
                            Userrole = "student",
                            MobileNumber = model.ContactNumber,
                            WhatsAppNumber = model.ContactNumber,
                            Address = "null",
                            City = "null",
                            State = "null",
                            PinCode = "null",
                            District = "null",
                            IsActive = true,
                            PhotoPath = null,
                            DepartmentID = model.DepartmentID,
                            IsActiveDate = DateTime.Now
                        };

                        db.USERs.Add(existingUser);
                        db.SaveChanges();
                    }

                    var department = db.DEPARTMENTs.FirstOrDefault(d => d.DepartmentID == model.DepartmentID);
                    var university = db.UNIVERSITies.FirstOrDefault(u => u.UniversityID == department.Universityid);
                    if (university == null)
                    {
                        return Json(new { success = false, message = "University not found!" });
                    }

                    if (ProfileImage != null && ProfileImage.ContentLength > 0)
                    {
                        string uploadsFolder = Server.MapPath("~/uploads");
                        if (!Directory.Exists(uploadsFolder))
                        {
                            Directory.CreateDirectory(uploadsFolder);
                        }
                        string uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(ProfileImage.FileName);
                        string filePath = Path.Combine(uploadsFolder, uniqueFileName);
                        ProfileImage.SaveAs(filePath);
                        existingUser.PhotoPath = "/uploads/" + uniqueFileName;
                    }

                    var existingRegistration = db.ClubRegistrations.FirstOrDefault(cr => cr.UserID == existingUser.UserID && cr.ClubID == model.ClubID);
                    if (existingRegistration != null)
                    {
                        return Json(new { success = false, message = "User already registered for this club!" });
                    }

                    var club = db.CLUBS.FirstOrDefault(c => c.ClubID == model.ClubID);
                    if (club == null || club.AvailableClubCommitteeSeats <= 0)
                    {
                        return Json(new { success = false, message = "No available seats in this club!" });
                    }

                    var clubRegistration = new ClubRegistration
                    {
                        UserID = existingUser.UserID,
                        ClubID = model.ClubID,
                        FullName = existingUser.FirstName,
                        Email = existingUser.Email,
                        ContactNumber = existingUser.MobileNumber,
                        ProfileImagePath = existingUser.PhotoPath,
                        PreferredRole = model.PreferredRole,
                        RoleJustification = model.RoleJustification,
                        RegisteredAt = DateTime.Now,
                        ApprovalStatusID = 1,
                        DepartmentID = model.DepartmentID,
                        UniversityID = university.UniversityID
                    };

                    db.ClubRegistrations.Add(clubRegistration);
                    db.Entry(club).Reload();
                    if (club.AvailableClubCommitteeSeats <= 0)
                    {
                        return Json(new { success = false, message = "Seats are no longer available!" });
                    }
                    club.AvailableClubCommitteeSeats--;

                    db.SaveChanges();
                    transaction.Commit();

                    return Json(new { success = true, message = "Registration successful!" });
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    return Json(new { success = false, message = "An error occurred: " + ex.Message });
                }
            }
        }

        public ActionResult BrowseEvents(int clubId)
        {
            using (var db = new dummyclubsEntities())
            {
                var club = db.CLUBS.FirstOrDefault(c => c.ClubID == clubId);
                if (club != null)
                {
                    ViewBag.ClubName = club.ClubName;
                }

                var upcomingEvents = db.EVENTS
                    .Where(e => e.ClubID == clubId &&
                                (e.EventStatus == "Upcoming not posted" || e.EventStatus == "Upcoming posted") &&
                                e.EventEndDateAndTime >= DateTime.Now)
                    .ToList();


                foreach (var ev in upcomingEvents.Where(e => e.EventStatus == "Upcoming not posted"))
                {
                    ev.IsActive = true;
                    ev.EventStatus = "Upcoming posted";
                }
                db.SaveChanges();

                var upcomingDtos = upcomingEvents.Select(e => new EventDto
                {
                    EventID = e.EventID,
                    EventName = e.EventName,
                    EventPoster = e.EventPoster,
                    EventStartDateAndTime = e.EventStartDateAndTime,
                    EventEndDateAndTime = e.EventEndDateAndTime,
                    EventStatus = e.EventStatus,
                    IsActive = e.IsActive
                }).ToList();
                ViewBag.UpcomingEvents = upcomingDtos;

                var concludedEvents = db.EVENTS
                    .Where(e => e.ClubID == clubId &&
                                e.EventStatus == "Concluded" &&
                                e.EventEndDateAndTime < DateTime.Now)
                    .OrderByDescending(e => e.EventEndDateAndTime)
                    .ToList();

                var concludedDtos = concludedEvents.Select(e => new EventDto
                {
                    EventID = e.EventID,
                    EventName = e.EventName,
                    EventPoster = e.EventPoster,
                    EventEndDateAndTime = e.EventEndDateAndTime,
                    EventStatus = e.EventStatus,
                    IsActive = e.IsActive
                }).ToList();
                ViewBag.ConcludedEvents = concludedDtos;

                var concludedYears = concludedEvents
                    .Select(e => e.EventEndDateAndTime.Year)
                    .Distinct()
                    .OrderByDescending(y => y)
                    .ToList();
                ViewBag.ConcludedEventYears = concludedYears;

                var eventIds = upcomingEvents.Select(e => e.EventID).ToList();
                var commentDtos = db.Comments
                    .Where(c => eventIds.Contains(c.EventID))
                    .Select(c => new CommentDto
                    {
                        CommentID = c.CommentID,
                        EventID = c.EventID,
                        CommentText = c.CommentText
                    }).ToList();
                ViewBag.Comments = commentDtos;

                ViewBag.EventID = upcomingDtos.FirstOrDefault()?.EventID ?? 0;
            }

            return View();
        }

        public ActionResult EventDetails(int id)
        {
            var eventItem = db.EVENTS
                .Include("Comments")
                .Include("Comments.Comments1")
                .FirstOrDefault(e => e.EventID == id);

            if (eventItem == null)
            {
                return HttpNotFound();
            }

            eventItem.Comments = eventItem.Comments
                .Where(c => c.ParentCommentID == null)
                .ToList();

            string registrationLink = Url.Action("VerifyEnrollment", "Clubs", new { id = eventItem.EventID }, protocol: "https");
            registrationLink = registrationLink.Replace("localhost:44368", "61a7-123-63-49-246.ngrok-free.app");

            string qrCodeImage = GenerateQRCode(registrationLink);
            ViewBag.QRImage = qrCodeImage;

            return View(eventItem);
        }

        public ActionResult EventHighlights(int id)
        {
            var eventDetails = db.EVENTS
                .Include(e => e.EventWinners)
                .Include(e => e.EventPhotos)
                .FirstOrDefault(e => e.EventID == id);

            if (eventDetails == null)
                return View();

            return View(eventDetails);
        }

        [HttpPost]
        public ActionResult AddComment(Comment comment)
        {
            if (ModelState.IsValid)
            {
                comment.PostedDate = DateTime.Now;
                db.Comments.Add(comment);
                db.SaveChanges();
            }

            return RedirectToAction("EventDetails", "Clubs", new { id = comment.EventID });
        }

        [HttpPost]
        public async Task<ActionResult> RegisterUser(int eventId, string enrollmentId)
        {
            var eventEntity = await db.EVENTS.FindAsync(eventId);

            if (eventEntity == null || string.IsNullOrEmpty(eventEntity.RegistrationURL))
            {
                return Content("Registration link not found.");
            }

            return Redirect(eventEntity.RegistrationURL);
        }

        private string GenerateQRCode(string data)
        {
            using (var qrGenerator = new QRCodeGenerator())
            {
                var qrCodeData = qrGenerator.CreateQrCode(data, QRCodeGenerator.ECCLevel.Q);
                using (var qrCode = new QRCode(qrCodeData))
                {
                    using (var bitmap = qrCode.GetGraphic(20))
                    {
                        using (var ms = new MemoryStream())
                        {
                            bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                            var imageBytes = ms.ToArray();
                            return "data:image/png;base64," + Convert.ToBase64String(imageBytes);
                        }
                    }
                }
            }
        }

        public ActionResult VerifyEnrollment(int id)
        {
            ViewBag.EventID = id;
            return View();
        }

        [HttpPost]
        public ActionResult VerifyEnrollment(string enrollmentId, int eventId)
        {
            var eventDetails = db.EVENTS.FirstOrDefault(e => e.EventID == eventId);
            if (eventDetails != null)
            {
                Session["EventID"] = eventId;
                return Redirect(eventDetails.RegistrationURL);
            }

            return HttpNotFound();
        }

        [HttpPost]
        public ActionResult StoreResponse()
        {
            try
            {
                Request.InputStream.Position = 0;
                string json;
                using (var reader = new StreamReader(Request.InputStream))
                    json = reader.ReadToEnd();

                var model = JsonConvert.DeserializeObject<RegistrationModel>(json);
                if (model == null)
                    return new HttpStatusCodeResult(400, "Invalid payload");

                var @event = db.EVENTS.FirstOrDefault(e => e.EventName == model.EventName);
                if (@event == null)
                    return new HttpStatusCodeResult(404, "Event not found");

                var entity = new EventRegistration
                {
                    EventID = @event.EventID,
                    TypeOfParticipation = model.ParticipationType,
                    GroupName = string.IsNullOrEmpty(model.GroupName) ? null : model.GroupName,
                    NumberOfMembers = model.ParticipationType == "Group"
                            && int.TryParse(model.NumMembers, out var m)
                        ? (int?)m
                        : null,
                    MemberNames = string.IsNullOrEmpty(model.MemberNames) ? null : model.MemberNames,
                    FullName = model.FullName,
                    Email = model.Email,
                    PhoneNumber = string.IsNullOrEmpty(model.PhoneNumber) ? null : model.PhoneNumber,
                    EnrollmentId = model.EnrollmentId,
                    UniversityName = string.IsNullOrEmpty(model.UniversityName) ? null : model.UniversityName,
                    Branch = string.IsNullOrEmpty(model.Branch) ? null : model.Branch,
                    YearOfStudy = string.IsNullOrEmpty(model.YearOfStudy) ? null : model.YearOfStudy,
                    RegisteredAt = DateTime.Now
                };

                db.EventRegistrations.Add(entity);
                db.SaveChanges();

                return Json(new { success = true });
            }
            catch (DbEntityValidationException ex)
            {
                var errorMessages = ex.EntityValidationErrors
                    .SelectMany(x => x.ValidationErrors)
                    .Select(x => $"Property: {x.PropertyName} Error: {x.ErrorMessage}")
                    .ToList();

                Response.StatusCode = 400;
                return Json(new { success = false, message = "Validation failed", errors = errorMessages });
            }
            catch (DbUpdateException dbEx)
            {
                var sqlMessage = dbEx.InnerException?.InnerException?.Message
                                 ?? dbEx.InnerException?.Message
                                 ?? dbEx.Message;

                Response.StatusCode = 500;
                return Json(new
                {
                    success = false,
                    message = "Database update failed",
                    sqlMessage = sqlMessage
                });
            }
            catch (Exception ex)
            {
                var innerMessage = ex.InnerException?.Message;

                Response.StatusCode = 500;
                return Json(new
                {
                    success = false,
                    message = ex.Message,
                    innerMessage = innerMessage
                });
            }
        }

        public ActionResult ConcludedEvents(int? year)
        {
            var query = db.EVENTS
                           .Where(e => e.EventStatus == "Concluded");

            var years = query
                .Select(e => e.EventEndDateAndTime.Year)
                .Distinct()
                .OrderByDescending(y => y)
                .ToList();

            if (year.HasValue)
            {
                query = query.Where(e => e.EventEndDateAndTime.Year == year.Value);
            }

            var model = new ConcludedEventsViewModel
            {
                Years = years,
                SelectedYear = year,
                Events = query
                           .OrderByDescending(e => e.EventEndDateAndTime)
                           .ToList()
            };

            return View(model);
        }

        // =======================
        // 🔹 NEW ACTIONS ADDED
        // =======================

        // GET: Clubs/Magazine
        public ActionResult Magazine()
        {
            /*
             * You can later extend this method to:
             * 1. Fetch magazine issues stored in a MAGAZINE table.
             * 2. Load magazine PDFs or cover images from the server.
             * 3. Provide filtering by year/volume.
             */
            ViewBag.Title = "Research - Magazine";
            return View();
        }

        // GET: Clubs/Newsletters
        public ActionResult Newsletters()
        {
            return View();
        }
    }
}
