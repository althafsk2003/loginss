using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace WebApplication4.Models
{
    public class EventDetailsViewModel
    {
        public EVENT Event { get; set; }
        public List<EventPhoto> EventPhotos { get; set; } = new List<EventPhoto>();
        public List<EventWinner> EventWinners { get; set; } = new List<EventWinner>();
        public List<EventVideo> EventVideos { get; set; } = new List<EventVideo>(); // initialize
    }

}