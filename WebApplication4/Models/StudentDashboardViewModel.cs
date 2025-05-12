using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace WebApplication4.Models
{
    public class StudentDashboardViewModel
    {
        public string EnrollmentId { get; set; }

        public string FullName { get; set; }
        public string Email { get; set; }
        public List<EventRegistrationViewModel> RegisteredEvents { get; set; }

        // Add this property to store the serialized events
        public string SerializedEvents { get; set; }
    }
}