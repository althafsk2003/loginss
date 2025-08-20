using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace WebApplication4.Models
{
    public partial class DEPARTMENT
    {

        [NotMapped]
        public bool? HasDirector { get; set; }
    }
}