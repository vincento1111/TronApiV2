using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TronApi;

namespace TronApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ChatController : ControllerBase
    {

        private readonly DataContext _context;

        public ChatController(DataContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<UserChats>>> GetAllMessages()
        {
            return await _context.UserChats.ToListAsync();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<UserChats>> GetMessage(int id)
        {
            var userChats = await _context.UserChats.FindAsync(id);

            if (userChats == null)
            {
                return NotFound();
            }

            return userChats;
        }



        [HttpPost]
        public async Task<ActionResult<UserChats>> PostMessage(UserChats userChats)
        {
            Console.WriteLine("hey vincent, er du en spasser?");

            var user = await _context.Users.FindAsync(userChats.UserId);
            if (user == null)
            {
                return BadRequest("User not found");
            }
            userChats.User = user;
            userChats.TimeStamp = DateTime.UtcNow;

            _context.UserChats.Add(userChats);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetMessage), new { id = userChats.MessageId }, userChats);
        }



    }
}

