using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace TronApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserStatsController : ControllerBase
    {

        private readonly DataContext _context;

        public UserStatsController(DataContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<List<UserStats>>> Get()
        {
            return Ok(await _context.UsersStats.ToListAsync());
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<List<UserStats>>> Get(int id)
        {
            var stats = await _context.UsersStats.FindAsync(id);
            if (stats == null)
                return BadRequest("404 hero not found");
            return Ok(stats);
        }

        [HttpPost]
        public async Task<ActionResult<List<UserStats>>> AddStats(UserStats stats)
        {
            _context.UsersStats.Add(stats);
            await _context.SaveChangesAsync();

            return Ok(await _context.UsersStats.ToListAsync());
        }

        [HttpPut("{userId}/{statType}")]
        public async Task<ActionResult<UserStats>> UpdateStats(int userId, int statType)
        {
            var dbStats = await _context.UsersStats.FirstOrDefaultAsync(u => u.UserId == userId);
            if (dbStats == null)
                return NotFound("stats not found");

            switch (statType)
            {
                case 1:
                    dbStats.Strength++;
                    break;
                case 2:
                    dbStats.Speed++;
                    break;
                case 3:
                    dbStats.Defense++;
                    break;
                case 4:
                    dbStats.Dexterity++;
                    break;
                default:
                    return BadRequest("Invalid stat type");
            }

            await _context.SaveChangesAsync();

            return Ok(dbStats);
        }




        //[HttpPut]

        //public async Task<ActionResult<List<UserStats>>> UpdateStats(UserStats Request)
        //{
        //    var dbStats = await _context.UsersStats.FindAsync(Request.StatId);
        //    if (dbStats == null)
        //        return NotFound("stats not found");
        //    if(Request.Strength != 0)
        //    {
        //        dbStats.Strength = Request.Strength;
        //    }if(Request.Defense != 0)
        //    {
        //        dbStats.Defense = Request.Defense;
        //    }
        //    if(Request.Speed != 0)
        //    {
        //        dbStats.Speed = Request.Speed;
        //    }
        //    if (Request.Dexterity != 0)
        //    {
        //        dbStats.Dexterity = Request.Dexterity;
        //    }
        //    if (Request.Experience != 0)
        //    {
        //        dbStats.Experience = Request.Experience;
        //    }
        //    if (Request.Life != 0)
        //    {
        //        dbStats.Life = Request.Life;
        //    }
        //    if (Request.Money != 0)
        //    {
        //        dbStats.Money = Request.Money;
        //    }
        //    if (Request.Level != 0)
        //    {
        //        dbStats.Level = Request.Level;
        //    }
        //    await _context.SaveChangesAsync();

        //    return Ok(await _context.UsersStats.ToListAsync());
        //}

        [HttpDelete("{id}")]
        public async Task<ActionResult<List<UserStats>>> Delete(int id)
        {
            var dbStats = await _context.UsersStats.FindAsync(id);
            if (dbStats == null)
                return BadRequest("404 hero not found");

            _context.UsersStats.Remove(dbStats);
            await _context.SaveChangesAsync();
            return Ok(await _context.UsersStats.ToListAsync());
        }
    }
}
