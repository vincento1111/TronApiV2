public class User
{
    public int UserId { get; set; }
    public string Email { get; set; }
    public string Password { get; set; }
    public bool IsAdmin { get; set; } // Add this line
}