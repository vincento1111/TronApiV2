using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TronApi
{
    public class Profile
    {
        [Key]
        public int ProfileId { get; set; }
        [ForeignKey("User")]
        public int UserId { get; set; }
        public User? User { get; set; }

        public string ProfileDes { get; set; } = string.Empty;
        public Profile(int userId)
        {
            UserId = userId;
            ProfileDes = "";

        }
    }
   
}
