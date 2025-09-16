using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace WebApplication4.Models
{
    public class EventDetailsViewModel
    {
        public EVENT Event { get; set; }

        // All photos for the event
        public List<EventPhoto> EventPhotos { get; set; } = new List<EventPhoto>();

        // Filtered list of videos for convenience
        public List<EventPhoto> EventVideos
        {
            get
            {
                return EventPhotos?.Where(p => p.MediaType == "Video").ToList() ?? new List<EventPhoto>();
            }
        }

        // All winners
        public List<EventWinner> EventWinners { get; set; } = new List<EventWinner>();

        // Optional: Filtered list of photos only (excluding videos)
        public List<EventPhoto> OnlyPhotos
        {
            get
            {
                return EventPhotos?.Where(p => p.MediaType == "Photo").ToList() ?? new List<EventPhoto>();
            }
        }
    }
}
