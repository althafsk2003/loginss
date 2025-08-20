using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace WebApplication4.Models
{
    public class SubDepartmentInputViewModel
    {
        public string HODName { get; set; }
        public string HODEmail { get; set; }
        public List<string> SubDepartments { get; set; }
    }
}