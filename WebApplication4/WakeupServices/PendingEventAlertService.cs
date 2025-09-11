using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using WebApplication4.Models;

namespace WebApplication4.WakeupServices
{
    public class PendingEventAlertService
    {
        private readonly dummyclubsEntities _db = new dummyclubsEntities();

        // Entry method to send pending event alerts
        public async Task SendPendingEventAlerts()
        {
            var now = DateTime.Now;

            // Pending statuses: 1 = Pending Mentor, 4 = Pending SCC/HOD, 7 = Pending Director
            int[] pendingStatuses = { 1, 4, 7 };

            // Fetch events pending > 32 hours
            var pendingEvents = _db.EVENTS
                .Where(e => pendingStatuses.Contains(e.ApprovalStatusID)
                            && DbFunctions.DiffHours(e.EventCreatedDate, now) >= 0)
                .ToList();

            foreach (var ev in pendingEvents)
            {
                try
                {
                    // Determine recipient and role
                    string recipientEmail, recipientRole;
                    GetRecipientInfo(ev, out recipientEmail, out recipientRole);

                    if (!string.IsNullOrEmpty(recipientEmail))
                    {
                        await SendAlertAsync(ev, recipientEmail, recipientRole);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error sending alert for EventID {ev.EventID}: {ex.Message}");
                }
            }
        }

        // Determine email and role for the event
        private void GetRecipientInfo(EVENT ev, out string email, out string role)
        {
            email = null;
            role = null;

            // Mentor
            if (ev.ApprovalStatusID == 1)
            {
                var club = _db.CLUBS.FirstOrDefault(c => c.ClubID == ev.ClubID);
                if (club != null)
                {
                    var mentorLogin = _db.Logins.FirstOrDefault(l => l.LoginID == club.MentorID);
                    if (mentorLogin != null)
                    {
                        email = mentorLogin.Email;
                        role = "Mentor";
                    }
                }
                return;
            }

            // HOD / SubHOD
            if (ev.ApprovalStatusID == 4)
            {
                var club = _db.CLUBS.FirstOrDefault(c => c.ClubID == ev.ClubID);
                if (club == null) return;

                if (club.SubDepartmentID != null)
                {
                    var subDept = _db.SUBDEPARTMENTs.FirstOrDefault(s => s.SubDepartmentID == club.SubDepartmentID);
                    if (subDept != null)
                    {
                        var subHodLogin = _db.Logins.FirstOrDefault(l => l.Email == subDept.HOD_Email);
                        if (subHodLogin != null)
                        {
                            email = subHodLogin.Email;
                            role = "SubHOD";
                        }
                    }
                }
                else
                {
                    var dept = _db.DEPARTMENTs.FirstOrDefault(d => d.DepartmentID == club.DepartmentID);
                    if (dept != null)
                    {
                        var hodLogin = _db.Logins.FirstOrDefault(l => l.Email == dept.HOD_Email);
                        if (hodLogin != null)
                        {
                            email = hodLogin.Email;
                            role = "HOD";
                        }
                    }
                }
                return;
            }

            // Director
            if (ev.ApprovalStatusID == 7)
            {
                var club = _db.CLUBS.FirstOrDefault(c => c.ClubID == ev.ClubID);
                if (club == null || club.SubDepartmentID == null) return;

                var subDept = _db.SUBDEPARTMENTs.FirstOrDefault(s => s.SubDepartmentID == club.SubDepartmentID);
                if (subDept != null)
                {
                    var directorDept = _db.DEPARTMENTs.FirstOrDefault(d => d.DepartmentID == subDept.DepartmentID);
                    if (directorDept != null)
                    {
                        var directorLogin = _db.Logins.FirstOrDefault(l => l.Email == directorDept.DirectorEmail);
                        if (directorLogin != null)
                        {
                            email = directorLogin.Email;
                            role = "Director";
                        }
                    }
                }
            }
        }

        // Send email using EmailService
        private async Task SendAlertAsync(EVENT ev, string recipientEmail, string recipientRole)
        {
            string subject = $"Pending Event Alert: {ev.EventName}";

            var links = GenerateActionLinks(ev, recipientRole);

            string body = $@"
<div style='font-family: Arial; font-size: 14px;'>
    <h3>Event Pending for Review: {ev.EventName}</h3>
    <p><strong>Description:</strong> {ev.EventDescription}</p>
    <p><strong>Dates:</strong> {ev.EventStartDateAndTime:dd-MMM-yyyy} - {ev.EventEndDateAndTime:dd-MMM-yyyy}</p>
    <p><strong>Venue:</strong> {ev.Venue}</p>
    <p><strong>Budget:</strong> {ev.EventBudget}</p>
    <p>
        <a href='{links.primaryHref}' style='padding:8px 12px; background-color:green; color:white; text-decoration:none;'>{links.primaryText}</a>
        <a href='{links.secondaryHref}' style='padding:8px 12px; background-color:red; color:white; text-decoration:none;'>{links.secondaryText}</a>
    </p>
    <p>This event has been pending for more than 32 hours.</p>
</div>";

            var emailService = new EmailService();
            await emailService.SendEmailAsync(recipientEmail, subject, body);
        }

        // Generate action links for email buttons
        // Generate action links for email buttons
        private (string primaryHref, string secondaryHref, string primaryText, string secondaryText) GenerateActionLinks(EVENT ev, string recipientRole)
        {
            string token = SecureHelper.Encrypt($"{ev.EventID}|{ev.ClubID}|email");
            string baseUrl = "https://localhost:44368"; // Replace with production host

            string primaryHref = "#";
            string secondaryHref = "#";
            string primaryText = "Approve";
            string secondaryText = "Reject";

            switch (recipientRole)
            {
                case "Mentor":
                    // Mentor should only see Forward + Reject
                    primaryHref = $"{baseUrl}/Mentor/ForwardEventToHOD?token={token}";
                    secondaryHref = $"{baseUrl}/Mentor/RejectEventRequest?token={token}";
                    primaryText = "Forward to HOD";
                    secondaryText = "Reject";
                    break;

                case "HOD":
                    primaryHref = $"{baseUrl}/HOD/ApproveEvent?token={token}";
                    secondaryHref = $"{baseUrl}/HOD/RejectEvent?token={token}";
                    primaryText = "Approve";
                    secondaryText = "Reject";
                    break;

                case "SubHOD":
                    primaryHref = $"{baseUrl}/SubHOD/ForwardToDirector?token={token}";
                    secondaryHref = $"{baseUrl}/SubHOD/RejectEvent?token={token}";
                    primaryText = "Forward to Director";
                    secondaryText = "Reject";
                    break;

                case "Director":
                    primaryHref = $"{baseUrl}/Director/DirectorApproveEvent?token={token}";
                    secondaryHref = $"{baseUrl}/Director/DirectorRejectEvent?token={token}";
                    primaryText = "Approve";
                    secondaryText = "Reject";
                    break;

                default:
                    break;
            }

            return (primaryHref, secondaryHref, primaryText, secondaryText);
        }



        // Check if the next approver after Mentor is HOD or SubHOD
        private bool IsNextApproverHOD(EVENT ev)
        {
            var club = _db.CLUBS.FirstOrDefault(c => c.ClubID == ev.ClubID);
            return club == null || club.SubDepartmentID == null;
        }
    }
}
