using System.Collections.Generic;
using System.Web.Mvc;

namespace WebApplication4.Models
{
    public class SubHODEventReviewViewModel
    {
        public int SelectedClubId { get; set; }

        public List<SelectListItem> ActiveClubs { get; set; }

        public List<EVENT> Events { get; set; } = new List<EVENT>(); // Add this
    }
}
