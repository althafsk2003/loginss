using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web;

namespace WebApplication4.Models
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext()
            : base("name=MyDbContext") // Matches the connection string in Web.config
        {
        }

        public DbSet<USER> USERs { get; set; }
    }
}