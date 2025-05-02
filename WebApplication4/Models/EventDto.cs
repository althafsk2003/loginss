using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace WebApplication4.Models
{
    public class EventDto
    {
        public int EventID { get; set; }
        public string EventName { get; set; }
        public string EventStatus { get; set; }
        public DateTime EventStartDateAndTime { get; set; }
        public DateTime EventEndDateAndTime { get; set; }
        public Nullable<bool> IsActive { get; set; } // Ensure bool, not bool?

        public string EventPoster { get; set; } // Add this if your view or JSON expects it
    }

}