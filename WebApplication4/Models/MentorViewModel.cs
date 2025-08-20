using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace WebApplication4.Models
{
    public class MentorViewModel
    {
        public int LoginID { get; set; }
        public string FullName { get; set; } // From Users table
        public string Email { get; set; }    // From Logins table
        public string Mobile { get; set; }   // From Users table
        public bool IsActive { get; set; }   // From Logins table
        public string DepartmentName { get; set; }
        public string SubDepartmentName { get; set; }
    }

}