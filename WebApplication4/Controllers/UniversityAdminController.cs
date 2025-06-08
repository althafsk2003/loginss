﻿using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using WebApplication4.Models;
using System.Dynamic;
using Org.BouncyCastle.Crypto.Generators;
using System.Drawing;
using System.Web.UI.WebControls;
using Org.BouncyCastle.Utilities;

namespace WebApplication4.Controllers
{
    public class UniversityAdminController : Controller
    {
        private readonly dummyclubsEntities _db = new dummyclubsEntities();
        private readonly EmailService _emailService = new EmailService();  // Injecting EmailService

        public async Task<ActionResult> Index()
        {
            if (!IsUniversityAdminLoggedIn())
            {
                return RedirectToAction("Login", "Admin");
            }

            int universityID = GetUniversityID();

            var university = await _db.UNIVERSITies
                .FirstOrDefaultAsync(u => u.UniversityID == universityID);

            var departments = await _db.DEPARTMENTs
                .Where(d => d.Universityid == universityID)
                .ToListAsync();

            var departmentIds = departments.Select(d => d.DepartmentID).ToList();

            var mentors = await _db.USERs
                .Where(u => u.Userrole == "Mentor" && u.DepartmentID != null && departmentIds.Contains(u.DepartmentID.Value))
                .ToListAsync();

            ViewBag.University = university;
            ViewBag.Departments = departments;
            ViewBag.Mentors = mentors;

            return View();
        }


        // ✅ Add School (Department)
        public ActionResult AddDepartment()
        {
            if (!IsUniversityAdminLoggedIn())
            {
                return RedirectToAction("Login", "Admin");
            }

            return View(new List<DEPARTMENT>());
        }

        [HttpPost]
        public async Task<ActionResult> AddDepartment(List<DEPARTMENT> Departments)
        {
            if (!IsUniversityAdminLoggedIn())
            {
                return RedirectToAction("Login", "Admin");
            }

            if (!ModelState.IsValid)
            {
                ViewBag.ErrorMessage = "Invalid input. Please fill all required fields.";
                return View(Departments);
            }

            try
            {
                int universityID = GetUniversityID();

                foreach (var dept in Departments)
                {
                    dept.Universityid = universityID;
                    dept.createdDate = DateTime.Now;
                    dept.IsActive = true;
                    dept.IsActiveDate = DateTime.Now;

                    _db.DEPARTMENTs.Add(dept);
                    await _db.SaveChangesAsync(); // Save to get DepartmentID for HOD login

                    // ✅ Create HOD Login Entry
                    var hodLogin = new Models.Login
                    {
                        Email = dept.HOD_Email,
                        PasswordHash = "Hod@123", // Ideally, hash the password
                        Role = "HOD",
                        IsActive=true,
                        DepartmentID = dept.DepartmentID,
                        UniversityID = dept.Universityid,
                        CreatedDate = DateTime.Now
                    };

                    _db.Logins.Add(hodLogin);
                    await _db.SaveChangesAsync();

                    // ✅ Send welcome email
                    string subject = "Welcome to Our Platform!";
                    string body = $"Hello {dept.HOD},<br/><br/>" +
                                  $"You have been successfully added as a HOD. Here are your login details:<br/>" +
                                  $"<strong>Username:</strong> {dept.HOD_Email}<br/>" +
                                  $"<strong>Password:</strong> Hod@123 (Please change your password upon login).<br/><br/>" +
                                  "Please log in and complete your profile.";

                    await _emailService.SendEmailAsync(dept.HOD_Email, subject, body);
                }

                TempData["SuccessMessage"] = "Departments and HOD logins added successfully!";
                return RedirectToAction("ManageDepartments");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in AddDepartment: {ex.Message}");
                ViewBag.ErrorMessage = "An error occurred while adding the departments.";
                return View(Departments);
            }
        }


        // ✅ Manage Departments
        public async Task<ActionResult> ManageDepartments()
        {
            if (!IsUniversityAdminLoggedIn())
            {
                return RedirectToAction("Login", "Admin");
            }

            int universityID = GetUniversityID();
            var departments = await _db.DEPARTMENTs.Where(d => d.Universityid == universityID).ToListAsync();

            return View(departments);
        }

        [HttpPost]
        public async Task<ActionResult> DeactivateDepartment(int departmentId)
        {
            if (!IsUniversityAdminLoggedIn())
            {
                return RedirectToAction("Login", "Admin");
            }

            try
            {
                var department = await _db.DEPARTMENTs.FindAsync(departmentId);
                if (department != null)
                {
                    department.IsActive = false;
                    department.IsActiveDate = DateTime.Now;
                    await _db.SaveChangesAsync();
                    TempData["SuccessMessage"] = "School deactivated successfully!";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in DeactivateDepartment: {ex.Message}");
                TempData["ErrorMessage"] = "An error occurred while deactivating the school.";
            }

            return RedirectToAction("ManageDepartments");
        }

        [HttpPost]
        public async Task<ActionResult> ActivateDepartment(int departmentId)
        {
            if (!IsUniversityAdminLoggedIn())
            {
                return RedirectToAction("Login", "Admin");
            }

            try
            {
                var department = await _db.DEPARTMENTs.FindAsync(departmentId);
                if (department != null)
                {
                    department.IsActive = true;
                    department.IsActiveDate = DateTime.Now;
                    await _db.SaveChangesAsync();
                    TempData["SuccessMessage"] = "School activated successfully!";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in ActivateDepartment: {ex.Message}");
                TempData["ErrorMessage"] = "An error occurred while activating the school.";
            }

            return RedirectToAction("ManageDepartments");
        }

        // ✅ View Schools (Only for this University)
        public async Task<ActionResult> ViewSchools()
        {
            if (!IsUniversityAdminLoggedIn())
            {
                return RedirectToAction("Login", "Admin");
            }

            int universityId = GetUniversityID();
            var departments = await _db.DEPARTMENTs.Where(d => d.Universityid == universityId).ToListAsync();

            return View(departments);
        }

        // ✅ Utility: Check if University Admin is Logged In
        private bool IsUniversityAdminLoggedIn()
        {
            return Session["UserRole"] != null &&
                   (string)Session["UserRole"] == "UniversityAdministrator" &&
                   Session["UniversityID"] != null;
        }

        // ✅ Utility: Get University ID from Session
        private int GetUniversityID()
        {
            return Convert.ToInt32(Session["UniversityID"]);
        }






        // ✅ Add Mentor - GET method
        [HttpGet]
        public ActionResult AddMentor()
        {
            int universityId = GetUniversityID(); // Get logged-in university's ID

            // Load only active departments for this university
            ViewBag.Departments = new SelectList(_db.DEPARTMENTs
                .Where(d => d.Universityid == universityId && d.IsActive == true), "DepartmentID", "DepartmentName");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> AddMentor(USER model, HttpPostedFileBase Photo)
        {
            if (!IsUniversityAdminLoggedIn())
            {
                return RedirectToAction("AddMentor", "UniversityAdmin");
            }

            if (!ModelState.IsValid)
            {
                // Repopulate departments dropdown if form submission fails
                int universityId = GetUniversityID();
                ViewBag.Departments = new SelectList(_db.DEPARTMENTs
                    .Where(d => d.Universityid == universityId && d.IsActive == true), "DepartmentID", "DepartmentName");

                return View(model);
            }

            try
            {
                // ✅ Upload photo if provided
                if (Photo != null && Photo.ContentLength > 0)
                {
                    string uploadDir = Server.MapPath("~/Uploads/");

                    if (!Directory.Exists(uploadDir))
                    {
                        Directory.CreateDirectory(uploadDir);
                    }

                    string fileName = Path.GetFileName(Photo.FileName);
                    string path = Path.Combine(uploadDir, fileName);
                    Photo.SaveAs(path);
                    model.PhotoPath = "~/Uploads/" + fileName;
                }

                // Set registration and activation date
                model.RegistrationDate = DateTime.Now;
                model.IsActiveDate = DateTime.Now;

                // Ensure mentor is active when added
                model.IsActive = true;

                // Set user role as Mentor
                model.Userrole = "Mentor";
                //model.DepartmentID = DepartmentID; // ✅ Store selected department in Users table

                // ✅ Save mentor details in USER table
                _db.USERs.Add(model);
                await _db.SaveChangesAsync();

                // ✅ Create login credentials for the mentor (UniversityID should be included here)
                var login = new Models.Login
                {
                    Email = model.Email,
                    PasswordHash = "Mentor@123", // TODO: Hash this password in production
                    Role = "Mentor",
                    CreatedDate = DateTime.Now,
                    IsActive = true,
                    //DepartmentID = DepartmentID, // ✅ Store selected department in Users table
                    UniversityID = GetUniversityID() // Associate the mentor with the logged-in university
                };

                // ✅ Add login record to the Login table
                _db.Logins.Add(login);
                await _db.SaveChangesAsync();

                // ✅ Send welcome email to the new mentor
                string subject = "Welcome to Our Platform!";
                string body = $"Hello {model.FirstName},<br/><br/>" +
                              $"You have been successfully added as a mentor. Here are your login details:<br/>" +
                              $"<strong>Username:</strong> {model.Email}<br/>" +
                              $"<strong>Password:</strong> Mentor@123 (Please change your password upon login).<br/><br/>" +
                              "Please log in and complete your profile.";

                // Send email asynchronously
                await _emailService.SendEmailAsync(model.Email, subject, body);

                // ✅ Store success message in TempData
                TempData["SuccessMessage"] = "Mentor added successfully.";
                TempData.Keep("SuccessMessage");

                // Clear the model to reset the fields for a fresh form
                model = new USER();

                // Redirect back to the "AddMentor1" action to show success message
                return RedirectToAction("AddMentor", "UniversityAdmin");
            }
            catch (Exception ex)
            {
                // Repopulate departments dropdown in case of an error
                int universityId = GetUniversityID();
                ViewBag.Departments = new SelectList(_db.DEPARTMENTs
                    .Where(d => d.Universityid == universityId && d.IsActive == true), "DepartmentID", "DepartmentName");

                ViewBag.ErrorMessage = "Error: " + ex.Message;
                return View(model);
            }
        }






        //manage mentors
        public ActionResult ManageMentors()
        {
            if (!IsUniversityAdminLoggedIn())
            {
                return RedirectToAction("Login", "Admin");
            }

            int universityID = GetUniversityID();

            var mentors = _db.USERs
                .Where(u => u.Userrole == "Mentor" && _db.Logins
                    .Any(l => l.Email == u.Email && l.UniversityID == universityID))
                .ToList();  // Get all mentors, active and inactive

            return View(mentors);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeactivateMentor(string email)
        {
            if (!IsUniversityAdminLoggedIn())
            {
                return RedirectToAction("Login", "Admin");
            }

            int universityID = GetUniversityID();

            var mentor = _db.USERs.FirstOrDefault(m => m.Email == email);
            var login = _db.Logins.FirstOrDefault(l => l.Email == email && l.UniversityID == universityID);

            if (mentor != null && login != null)
            {
                mentor.IsActive = false;
                mentor.IsActiveDate = DateTime.Now;
                login.IsActive = false;

                _db.SaveChanges(); // Save both changes at once

                TempData["SuccessMessage"] = "Mentor has been deactivated successfully.";
            }
            else
            {
                TempData["ErrorMessage"] = "Mentor not found or does not belong to your university.";
            }

            return RedirectToAction("ManageMentors");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ActivateMentor(string email)
        {
            if (!IsUniversityAdminLoggedIn())
            {
                return RedirectToAction("Login", "Admin");
            }

            int universityID = GetUniversityID();

            var mentor = _db.USERs.FirstOrDefault(m => m.Email == email);
            var login = _db.Logins.FirstOrDefault(l => l.Email == email && l.UniversityID == universityID);

            if (mentor != null && login != null)
            {
                mentor.IsActive = true;
                mentor.IsActiveDate = DateTime.Now;
                login.IsActive = true;

                _db.SaveChanges(); // Save both changes at once

                TempData["SuccessMessage"] = "Mentor has been activated successfully.";
            }
            else
            {
                TempData["ErrorMessage"] = "Mentor not found or does not belong to your university.";
            }

            return RedirectToAction("ManageMentors");
        }




        // ViewMentors action for viewing all mentors of the logged-in admin's university
        public ActionResult ViewMentors()
        {
            if (!IsUniversityAdminLoggedIn())
            {
                return RedirectToAction("Login", "UniversityAdmin");
            }

            int universityID = GetUniversityID(); // Get the university ID of the logged-in admin

            // Fetch mentors directly using a join between USERs and Logins
            var mentors = (from user in _db.USERs
                           join login in _db.Logins
                           on user.Email equals login.Email
                           where login.UniversityID == universityID && login.Role == "Mentor"
                           select user).ToList();

            return View("ViewMentors", mentors);
        }

        public ActionResult ViewActiveMentors()
        {
            if (!IsUniversityAdminLoggedIn())
            {
                Debug.WriteLine("Admin not logged in - Redirecting to Login");
                return RedirectToAction("Login", "UniversityAdmin");
            }

            int universityID = GetUniversityID();
            Debug.WriteLine($"Current University ID: {universityID}");

            var activeMentors = _db.USERs
                .Join(_db.Logins,
                      user => user.Email,
                      login => login.Email,
                      (user, login) => new { User = user, Login = login })
                .Where(ul => ul.Login.UniversityID == universityID
                          && ul.Login.Role == "Mentor"
                          && ul.Login.IsActive == true
                          && ul.User.IsActive == true)
                .Select(ul => ul.User)
                .ToList();

            Debug.WriteLine($"Active Mentors Count: {activeMentors.Count}");

            return View("ViewMentors", activeMentors);
        }

        public ActionResult ViewDeactivatedMentors()
        {
            if (!IsUniversityAdminLoggedIn())
            {
                Debug.WriteLine("Admin not logged in - Redirecting to Login");
                return RedirectToAction("Login", "UniversityAdmin");
            }

            int universityID = GetUniversityID();
            Debug.WriteLine($"Current University ID: {universityID}");

            var deactivatedMentors = _db.USERs
                .Join(_db.Logins,
                      user => user.Email,
                      login => login.Email,
                      (user, login) => new { User = user, Login = login })
                .Where(ul => ul.Login.UniversityID == universityID
                          && ul.Login.Role == "Mentor"
                          && ul.Login.IsActive == false
                          && ul.User.IsActive == false)
                .Select(ul => ul.User)
                .ToList();

            Debug.WriteLine($"Deactivated Mentors Count: {deactivatedMentors.Count}");

            return View("ViewMentors", deactivatedMentors);
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

        public ActionResult ViewAllDepartments()
        {
            int universityID = GetUniversityID();

            var departments = _db.DEPARTMENTs
                .Where(d => d.Universityid == universityID)
                .ToList();

            return View(departments); // Views/UniversityAdmin/ViewAllDepartments.cshtml
        }
        public ActionResult ViewClubs(int departmentId)
        {
            var clubs = _db.CLUBS
                .Where(c => c.DepartmentID == departmentId)
                .ToList();

            ViewBag.Department = _db.DEPARTMENTs.Find(departmentId);
            return View(clubs); // Views/UniversityAdmin/ViewClubs.cshtml
        }
        [HttpGet]
        public ActionResult ViewEvents(int clubId)
        {
            int universityID = GetUniversityID();

            // Join CLUB with DEPARTMENT to validate ownership
            var clubWithDept = (from c in _db.CLUBS
                                join d in _db.DEPARTMENTs on c.DepartmentID equals d.DepartmentID
                                where c.ClubID == clubId && d.Universityid == universityID
                                select new
                                {
                                    Club = c,
                                    Department = d
                                }).FirstOrDefault();

            if (clubWithDept == null)
            {
                return HttpNotFound();
            }

            var events = _db.EVENTS
                .Where(e => e.ClubID == clubId)
                .Select(e => new EventDetailsViewsModel
                {
                    EventID = e.EventID,
                    EventName = e.EventName,
                    EventDescription = e.EventDescription,
                    EventDate = (DateTime)e.EventCreatedDate,
                    Venue = "ICFAI Foundation, Hyderabad"
                }).ToList();

            var model = new ClubDetailsViewModel
            {
                ClubID = clubWithDept.Club.ClubID,
                ClubName = clubWithDept.Club.ClubName,
                Description = clubWithDept.Club.Description,
                Events = events
            };

            return View(model);
        }




    }
}
