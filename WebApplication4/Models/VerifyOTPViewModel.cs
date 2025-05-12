using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace WebApplication4.Models
{
    public class VerifyOTPViewModel
    {
        public string Email { get; set; }

        public string OTP { get; set; }

        public string NewPassword { get; set; }

        public string ConfirmPassword { get; set; }
    }

}