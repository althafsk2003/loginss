using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace WebApplication4.Models
{
    public class SubHodDashboardViewModel
    {
        public List<SubHodViewModel> TableData { get; set; } = new List<SubHodViewModel>();
        public bool ShowToggle { get; set; } // For enabling/disabling toggle column

        public int TotalCount { get; set; }
        public int ActiveCount { get; set; }
        public int InactiveCount { get; set; }
    }
}