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
                .Select(c => new { c.ClubID, c.ClubName, c.LogoImagePath }) // Keep the original path
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

            // Fetching Department Name based on DepartmentID
            var department = db.DEPARTMENTs.FirstOrDefault(d => d.DepartmentID == club.DepartmentID);

            // Fetching Mentor's Email from Logins table
            var mentorLogin = db.Logins.FirstOrDefault(m => m.LoginID == club.MentorID);

            // Fetching Mentor's Name from Users table using the Email
            var mentorUser = db.USERs.FirstOrDefault(u => u.Email == mentorLogin.Email);

            // Storing values in ViewBag
            ViewBag.DepartmentName = department != null ? department.DepartmentName : "Unknown";
            ViewBag.MentorName = mentorUser != null ? mentorUser.FirstName : "Unknown";
            ViewBag.MentorEmail = mentorLogin != null ? mentorLogin.Email : "Unknown";


            // --- Set University and Departments ---
            // First, fetch the university based on the club's department.
            var university = department != null
                ? db.UNIVERSITies.FirstOrDefault(u => u.UniversityID == department.Universityid)
                : null;

            // Fetch all departments under this university
            var departments = university != null
? db.DEPARTMENTs
.Where(d => d.Universityid == university.UniversityID)
.Select(d => new DepartmentDto { Id = d.DepartmentID, Name = d.DepartmentName })
.ToList()
: new List<DepartmentDto>();



            // Assign values to ViewBag so they are available in the view (and in your modal)
            ViewBag.UniversityName = university?.UniversityNAME ?? "Unknown";
            ViewBag.UniversityId = university?.UniversityID; // Hidden ID for Form Submission
            ViewBag.Departments = departments;
            // --- End of University and Departments assignment ---

            // Optionally, for debugging
            // System.Diagnostics.Debug.WriteLine("University: " + ViewBag.UniversityName);



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

            System.Diagnostics.Debug.WriteLine("This is a trace message");
            System.Diagnostics.Debug.WriteLine("Profile Image: " + (ProfileImage != null ? ProfileImage.FileName : "No file uploaded"));
            System.Diagnostics.Debug.WriteLine("Model Data: " + JsonConvert.SerializeObject(model));
            System.Diagnostics.Debug.WriteLine($"ClubID: {model.ClubID}, DepartmentID: {model.DepartmentID}, UniversityID: {model.UniversityID}");


            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors)
                                              .Select(e => e.ErrorMessage)
                                              .ToList();

                System.Diagnostics.Debug.WriteLine("Validation Errors: " + string.Join(", ", errors));

                return Json(new { success = false, message = "Invalid data!", errors });
            }


            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    // 1️⃣ Check if User Already Exists
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

                    // 2️⃣ Fetch the University ID from the Department
                    var department = db.DEPARTMENTs.FirstOrDefault(d => d.DepartmentID == model.DepartmentID);
                    var university = db.UNIVERSITies.FirstOrDefault(u => u.UniversityID == department.Universityid);
                    if (university == null)
                    {
                        return Json(new { success = false, message = "University not found!" });
                    }

                    // 3️⃣ Upload Profile Image
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

                    // 4️⃣ Check if User is Already Registered in the Club
                    var existingRegistration = db.ClubRegistrations.FirstOrDefault(cr => cr.UserID == existingUser.UserID && cr.ClubID == model.ClubID);
                    if (existingRegistration != null)
                    {
                        return Json(new { success = false, message = "User already registered for this club!" });
                    }

                    // 5️⃣ Validate Club Seat Availability
                    var club = db.CLUBS.FirstOrDefault(c => c.ClubID == model.ClubID);
                    if (club == null || club.AvailableClubCommitteeSeats <= 0)
                    {
                        return Json(new { success = false, message = "No available seats in this club!" });
                    }

                    // 6️⃣ Save Club Registration
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

                    // 7️⃣ Reduce Club Seats with **Concurrency Handling**
                    db.Entry(club).Reload(); // Ensure the latest data
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
                // Fetch Club Name
                var club = db.CLUBS.FirstOrDefault(c => c.ClubID == clubId);
                if (club != null)
                {
                    ViewBag.ClubName = club.ClubName;
                }

                // Get upcoming events
                var upcomingEvents = db.EVENTS
                    .Where(e => e.ClubID == clubId &&
                                (e.EventStatus == "Upcoming not posted" || e.EventStatus == "Upcoming posted"))
                    .ToList();

                // Update those with "Upcoming not posted"
                foreach (var ev in upcomingEvents.Where(e => e.EventStatus == "Upcoming not posted"))
                {
                    ev.IsActive = true;
                    ev.EventStatus = "Upcoming posted";
                }
                db.SaveChanges();

                // Project upcoming events to DTO
                var upcomingDtos = upcomingEvents.Select(e => new EventDto
                {
                    EventID = e.EventID,
                    EventName = e.EventName,
                    EventPoster = e.EventPoster,
                    EventStartDateAndTime=e.EventStartDateAndTime,
                    EventEndDateAndTime = e.EventEndDateAndTime,
                    EventStatus = e.EventStatus,
                    IsActive = e.IsActive
                }).ToList();
                ViewBag.UpcomingEvents = upcomingDtos;

                // Get all concluded events
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

                // Extract distinct years for filter
                var concludedYears = concludedEvents
                    .Select(e => e.EventEndDateAndTime.Year)
                    .Distinct()
                    .OrderByDescending(y => y)
                    .ToList();
                ViewBag.ConcludedEventYears = concludedYears;

                // Load comments for all upcoming events
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

                // Set ViewBag.EventID for AJAX
                ViewBag.EventID = upcomingDtos.FirstOrDefault()?.EventID ?? 0;
            }

            return View();
        }




        // GET: Events/Details/5
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

            // Only include top-level comments
            eventItem.Comments = eventItem.Comments
                .Where(c => c.ParentCommentID == null)
                .ToList();

            // Generate QR code for the registration URL
            //string registrationLink = Url.Action("VerifyEnrollment", "Clubs", new { id = eventItem.EventID }, protocol: "http");

            // Replace localhost with your ngrok URL
            string registrationLink = Url.Action("VerifyEnrollment", "Clubs", new { id = eventItem.EventID }, protocol: "https");
            registrationLink = registrationLink.Replace("localhost:44368", "750d-123-63-49-246.ngrok-free.app");


            // Generate the QR code for this URL
            string qrCodeImage = GenerateQRCode(registrationLink);

            // Pass the QR image to the view
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

            // Redirect to the Google form URL
            return Redirect(eventEntity.RegistrationURL);
        }

        // Action to get the registration link based on EventID
        /* [HttpGet]
         public async Task<ActionResult> EventDetail(int eventId)
         {
             // Fetch the event details from the database using EventID
             var eventDetails = await db.EVENTS
                                          .Where(e => e.EventID == eventId)
                                          .FirstOrDefaultAsync();

             if (eventDetails == null)
             {
                 return View();
             }

             // Generate QR code for the registration link
             string registrationLink = eventDetails.RegistrationURL;
             string qrCodeImage = GenerateQRCode(registrationLink);

             // Log QR base64 string for debugging
             System.Diagnostics.Debug.WriteLine("QR Image: " + qrCodeImage);

             // Pass the event details along with the QR code image as a model to the view
             var model = new PostEventViewModel
             {
                 EventID = eventDetails.EventID,
                 EventName = eventDetails.EventName,
                 EventDescription = eventDetails.EventDescription,
                 RegistrationURL = registrationLink,
                 QRContentText = registrationLink,
                 QRImage = qrCodeImage // Set this property with the generated QR code image as base64 string
             };

             return View(model);
         }*/

        // Helper method to generate QR code
        private string GenerateQRCode(string data)
        {
            using (var qrGenerator = new QRCodeGenerator())
            {
                var qrCodeData = qrGenerator.CreateQrCode(data, QRCodeGenerator.ECCLevel.Q);
                using (var qrCode = new QRCode(qrCodeData))
                {
                    using (var bitmap = qrCode.GetGraphic(20))  // 20 is the pixel size
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

        // GET: Clubs/VerifyEnrollment
        public ActionResult VerifyEnrollment(int id)
        {
            ViewBag.EventID = id;
            return View();
        }

        // POST: Validate and redirect
        [HttpPost]
        public ActionResult VerifyEnrollment(string enrollmentId, int eventId)
        {
            // You can also validate enrollmentId if needed

            var eventDetails = db.EVENTS.FirstOrDefault(e => e.EventID == eventId);
            if (eventDetails != null)
            {
                Session["EventID"] = eventId;
                return Redirect(eventDetails.RegistrationURL); // Redirect to actual registration form
            }

            return HttpNotFound();
        }



        [HttpPost]

    
        public ActionResult StoreResponse()
        {
            try
            {
                // 1. Read request body
                Request.InputStream.Position = 0;
                string json;
                using (var reader = new StreamReader(Request.InputStream))
                    json = reader.ReadToEnd();

                // 2. Deserialize into model
                var model = JsonConvert.DeserializeObject<RegistrationModel>(json);
                if (model == null)
                    return new HttpStatusCodeResult(400, "Invalid payload");


                // ✅ Fetch EventID from DB using EventName
                var @event = db.EVENTS.FirstOrDefault(e => e.EventName == model.EventName);
                if (@event == null)
                    return new HttpStatusCodeResult(404, "Event not found");

                // 3. Map to EF entity
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

                // 4. Save to database
                db.EventRegistrations.Add(entity);
                db.SaveChanges();

                // ✅ 5. Send SMS
                //SendSmsToStudent(model.PhoneNumber, model.FullName, model.EventName);

                // 5. Return success response
                return Json(new { success = true });
            }
            catch (DbEntityValidationException ex)
            {
                var errorMessages = ex.EntityValidationErrors
                    .SelectMany(x => x.ValidationErrors)
                    .Select(x => $"Property: {x.PropertyName} Error: {x.ErrorMessage}")
                    .ToList();

                Response.StatusCode = 400;  // Bad Request for validation errors
                return Json(new { success = false, message = "Validation failed", errors = errorMessages });
            }
            catch (DbUpdateException dbEx)
            {
                // Unwrap the inner exception to get detailed database error message
                var sqlMessage = dbEx.InnerException?.InnerException?.Message
                                 ?? dbEx.InnerException?.Message
                                 ?? dbEx.Message;

                Response.StatusCode = 500;  // Internal Server Error for DB update errors
                return Json(new
                {
                    success = false,
                    message = "Database update failed",
                    sqlMessage = sqlMessage  // Send detailed SQL error message
                });
            }
            catch (Exception ex)
            {
                var innerMessage = ex.InnerException?.Message;

                Response.StatusCode = 500;  // Internal Server Error for other exceptions
                return Json(new
                {
                    success = false,
                    message = ex.Message,
                    innerMessage = innerMessage  // Optionally send inner message
                });
            }
        }


       


        // GET: /Clubs/ConcludedEvents
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

    }
}