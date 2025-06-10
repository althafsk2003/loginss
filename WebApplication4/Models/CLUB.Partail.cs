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
        public string MentorName;
    }
}