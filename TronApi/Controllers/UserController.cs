using Microsoft.AspNetCore.Mvc;

namespace TronApi.Controllers
{
    
    [Route("api/users")]
    [ApiController]
    
    public class UserController : ControllerBase
    {

        private readonly DataContext _context;

        public UserController(DataContext context)
        {
            _context = context;
        }
        
        [HttpGet]
        public async Task<ActionResult<List<User>>> Get()
        {
            return Ok(await _context.Users.ToListAsync());
        }


        [HttpGet("{id}")]
        public async Task<ActionResult<List<User>>> Get(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
                return BadRequest("404 user not found");
            return Ok(user);
        }

        //GET
        [HttpGet("login")] //virker! 
        public async Task<ActionResult<IEnumerable<User>>> GetLogin(string email, string password)
        {

            //var userEmail = await _context.Users.Where(u => u.Email == email).ToListAsync();

            var temp = await _context.Users.FirstOrDefaultAsync(u => u.Email == email && u.Password == password);


            if (temp == null)
            {
                Console.WriteLine("NOT FOUND!!!");
                return NotFound();

            }
            else return Ok(temp);
            //else return Ok(temp);
        }

        [HttpGet("id")]
        public async Task<ActionResult<IEnumerable<User>>> GetUserId(string email)
        {
            var _user = await _context.Users.Where(u => u.Email == email).FirstOrDefaultAsync();
            if (_user == null)
            {
                return BadRequest();
            }
            
            else return Ok(_user.UserId);
        }

        [HttpPost]
        public async Task<ActionResult<List<User>>> AddUserAccount(User userAccount)
        {
            // Check if a user with the same email already exists
            var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == userAccount.Email);
            if (existingUser != null)
            {
                return BadRequest("A user with this email already exists.");
            }

            _context.Users.Add(userAccount);
            await _context.SaveChangesAsync();

            UserStats userStats = new UserStats(userAccount.UserId);
            _context.UsersStats.Add(userStats);
            await _context.SaveChangesAsync();

            Profile profiles = new Profile(userAccount.UserId);
            _context.Profiles.Add(profiles);
            await _context.SaveChangesAsync();

            return Ok(await _context.Users.ToListAsync());
        }

        [HttpPut]

        public async Task<ActionResult<List<User>>> UpdateUserAccount(User Request)
        {
            var dbUser = await _context.Users.FindAsync(Request.UserId);
            if (dbUser == null)
                return NotFound("User not found");

            dbUser.Email = Request.Email;
            dbUser.Password = Request.Password;


            await _context.SaveChangesAsync();

            return Ok(await _context.Users.ToListAsync());
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult<List<User>>> Delete(int id)
        {
            var dbUser = await _context.Users.FindAsync(id);
            var dbProfile = await _context.Profiles.FindAsync(id);
            var dbUserStats = await _context.UsersStats.FindAsync(id);
            var dbInventory = await _context.UserInventories.FindAsync(id);
            if (dbUser == null)
                return BadRequest("404 hero not found");

            _context.Users.Remove(dbUser);
            _context.Profiles.Remove(dbProfile);
            _context.UsersStats.Remove(dbUserStats);
            _context.UserInventories.Remove(dbInventory);
            await _context.SaveChangesAsync();
            return Ok(await _context.Users.ToListAsync());
        }
    }
}
