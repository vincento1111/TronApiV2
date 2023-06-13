using System.ComponentModel.DataAnnotations.Schema;

namespace TronApi
{
    public class Profile
    {
        public int ProfileId { get; set; }
        [ForeignKey("User")]
        public int UserId { get; set; }
        public string ProfileDes { get; set; } = string.Empty;
        public Profile(int userId)
        {
            UserId = userId;
            ProfileDes = "";

        }
    }
   
}
