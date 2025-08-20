using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace WebApplication4.Models
{

    public class ClubDashboardViewModel
    {
        public List<ClubViewModel> TableData { get; set; }
        public bool ShowToggle { get; set; }
        public int TotalCount { get; set; }
        public int ActiveCount { get; set; }
        public int InactiveCount { get; set; }
    }
}