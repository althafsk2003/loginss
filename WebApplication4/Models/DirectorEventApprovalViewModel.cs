using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Web;
using System.Web.Mvc;

namespace WebApplication4.Models
{
    public class DirectorEventApprovalViewModel
    {
        public int? SelectedClubId { get; set; }  // For dropdown selection of club

        public List<SelectListItem> ActiveClubs { get; set; } // Club dropdown list

        public List<EVENT> Events { get; set; }  // Events for selected club

        public EVENT Event { get; set; }         // Used when opening a single event for approval

        [Display(Name = "Upload Signed Document")]
        public HttpPostedFileBase SignedDocument { get; set; }

        [Display(Name = "Approved Budget Amount")]
        public decimal? ApprovedAmount { get; set; }

        [Display(Name = "Rejection Reason")]
        public string RejectionReason { get; set; }

    }
}
