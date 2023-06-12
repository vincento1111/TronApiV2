using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;


namespace TronApi.Hubs
{


    public class ChatHub : Hub
    {
        private readonly DataContext _context;

        public ChatHub(DataContext context)
        {
            _context = context;
        }

        public async Task SendMessage(UserChats chatData)
        {
            var user = await _context.Users.FindAsync(chatData.UserId);
            if (user == null)
            {
                return; // User not found
            }

            chatData.User = user;
            chatData.TimeStamp = DateTime.UtcNow;

            _context.UserChats.Add(chatData);
            await _context.SaveChangesAsync();

            // Notify all connected clients of the new message
            await Clients.All.SendAsync("ReceiveMessage", chatData);
        }



    }


}
