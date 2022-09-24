namespace TronApi
{
    public class Profile
    {
        public int ProfileId { get; set; }
        public int UserId { get; set; }
        public string ProfileDes { get; set; } = string.Empty;
        public Profile(int userId)
        {
            UserId = userId;
            ProfileDes = "";

        }
    }
   
}
