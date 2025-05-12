using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace WebApplication4.Models
{
    public class ClubDetailsViewModel
    {
        public int ClubID { get; set; }
        public string ClubName { get; set; }
        public string Description { get; set; }
        public List<EventDetailsViewsModel> Events { get; set; } = new List<EventDetailsViewsModel>();
    }
}