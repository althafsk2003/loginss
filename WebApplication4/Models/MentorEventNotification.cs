using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace WebApplication4.Models
{
    public class MentorEventNotification
    {
        public int EventID { get; set; }
        public string EventName { get; set; }
        public string Url { get; set; }
        public string Message { get; set; }

        // Add this property
        public int ApprovalStatusID { get; set; }
    }


}