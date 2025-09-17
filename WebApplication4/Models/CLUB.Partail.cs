using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace WebApplication4.Models
{
    public partial class CLUB
    {
        [NotMapped]
        public string MentorName { get; set; }

        [NotMapped]
        public string MentorEmail { get; set; }

        [NotMapped]
        public string MentorMobile { get; set; }

        [NotMapped]
        public string SubDepartmentName { get; set; }

        [NotMapped]
        public string SubDeptHOD { get; set; }

        [NotMapped]
        public string SubDeptHODEmail { get; set; }

        [NotMapped]
        public string DepartmentDirector { get; set; }

        [NotMapped]
        public string DepartmentDirectorEmail { get; set; }

        [NotMapped]
        public string DepartmentHOD { get; set; }

        [NotMapped]
        public string DepartmentHODEmail { get; set; }
    }
}