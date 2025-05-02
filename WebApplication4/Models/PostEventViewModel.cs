using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace WebApplication4.Models
{
    public class PostEventViewModel
    {
        public int EventID { get; set; }

        [Required]
        public string EventName { get; set; }

        [Required]
        public string EventDescription { get; set; }

        public string EventPoster { get; set; }

        [Required]
        public DateTime EventStartDateAndTime { get; set; }

        [Required]
        public DateTime EventEndDateAndTime { get; set; }

        [Required]
        public string Venue { get; set; }

        public string RegistrationURL { get; set; }

        public string QRContentText { get; set; }

        public string OrganizerName { get; set; }

        public string ClubName { get; set; }

        public HttpPostedFileBase EventPosterFile { get; set; } // Will receive the uploaded file

        public HttpPostedFileBase EventBannerFile { get; set; }

        public string EventBanner { get; set; }

        // Add this property for QR code image as a Base64 string
        public string QRImage { get; set; }

    }

}