using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace WebApplication4.Models
{
    public class EventDetailsViewsModel
    {

        public int EventID { get; set; }
        public string EventName { get; set; }
        public string EventDescription { get; set; }
        public System.DateTime EventDate { get; set; }
        public DateTime? RegisteredAt { get; set; }
        public string Venue { get; set; }
        public string Organizer { get; set; }
    }
}