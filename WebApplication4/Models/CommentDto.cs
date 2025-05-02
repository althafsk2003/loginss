using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace WebApplication4.Models
{
    public class CommentDto
    {

        public int CommentID { get; set; }
        public int EventID { get; set; }
        public string CommentText { get; set; }
        public string CommentedBy { get; set; }
        public DateTime CommentedOn { get; set; }
    }
}