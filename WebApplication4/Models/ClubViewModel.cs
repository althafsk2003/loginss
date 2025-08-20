using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace WebApplication4.Models
{
    public class ClubViewModel
    {
        public int ClubID { get; set; }
        public string ClubName { get; set; }
        public string Email { get; set; }
        public string DepartmentName { get; set; }
        public string SubDepartmentName { get; set; }
        public bool IsActive { get; set; }
    }
}