using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TronApi
{
    public class UserChats
    {
        [Key]
        public int MessageId { get; set; }

        [ForeignKey("User")]
        public int UserId { get; set; }

        public User? User { get; set; }

        public string Content { get; set; } = "";
        public DateTime TimeStamp { get; set; }
    }
}
