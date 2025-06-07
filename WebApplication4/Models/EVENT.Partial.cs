
namespace WebApplication4.Models
{
    using System.ComponentModel.DataAnnotations.Schema;

    public partial class EVENT
    {
        [NotMapped]
        public string ClubName;

        [NotMapped]
        public string Department;

        [NotMapped]
        public string University;

        [NotMapped]
        public string OrganizerName;
    }
}