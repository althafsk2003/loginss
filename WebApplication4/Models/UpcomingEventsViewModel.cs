using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace WebApplication4.Models
{
    public class UpcomingEventsViewModel
    {

        public List<EVENT> ApprovedNotPostedEvents { get; set; }
        public List<EVENT> PostedUpcomingEvents { get; set; }

    }
}