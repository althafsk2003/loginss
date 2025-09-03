using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace WebApplication4.Models
{
    public partial class EVENT
    {
        [NotMapped]
        public string ClubName;

        [NotMapped]
        public string Department;

        [NotMapped]
        public string University;

        [NotMapped]
        public string OrganizerName;
        [NotMapped]
        public string Token { get; set; }

    }
}