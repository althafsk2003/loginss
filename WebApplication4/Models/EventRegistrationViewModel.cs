using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace WebApplication4.Models
{
    public class EventRegistrationViewModel
    {
        public string EventName { get; set; }
        public string EventDescription { get; set; }  // Added EventDescription
        public DateTime? EventDate { get; set; }  // Added EventDate
        public string GroupName { get; set; }
        public string GroupLeader { get; set; }
        public List<GroupMember> GroupMembers { get; set; }
    }
}