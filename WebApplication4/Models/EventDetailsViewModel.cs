using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace WebApplication4.Models
{
    public class EventDetailsViewModel
    {

        public EVENT Event { get; set; }
        public List<EventPhoto> EventPhotos { get; set; }
        public List<EventWinner> EventWinners { get; set; }

        //public List<EventWinner> Winners { get; set; }  // Add the Winners list for new winners to be added
    

}
}