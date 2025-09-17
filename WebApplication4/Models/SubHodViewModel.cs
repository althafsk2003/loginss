using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace WebApplication4.Models
{
    public class SubHodViewModel
    {
        public int SubDepartmentId { get; set; } // ID of the sub-department
        public string HODName { get; set; }      // HOD responsible
        public string HODEmail { get; set; }     // HOD email
        public string DepartmentName { get; set; }
        public string SubDepartmentName { get; set; }
        public bool IsActive { get; set; }

    }
}