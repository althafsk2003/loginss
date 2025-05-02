using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Microsoft.Extensions.Logging;

namespace WebApplication4.Models
{
    public class ConcludedEventsViewModel
    {

        public List<int> Years { get; set; }
        public int? SelectedYear { get; set; }
        public List<EVENT> Events { get; set; }
    }            
}