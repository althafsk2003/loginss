using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace WebApplication4.Models
{
    public class RegistrationModel
    {

        public int EventID { get; set; }
        public string ParticipationType { get; set; }
        public string GroupName { get; set; }
        public string NumMembers { get; set; }
        public string MemberNames { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public string EnrollmentId { get; set; }
        public string UniversityName { get; set; }
        public string Branch { get; set; }
        public string YearOfStudy { get; set; }

        public string EventName { get; set; }
    }
}