
using Microsoft.AspNetCore.Mvc;

namespace TronApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProfileController : ControllerBase
    {
        private readonly DataContext _context;

        public ProfileController(DataContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<List<Profile>>> Get()
        {
            return Ok(await _context.Profiles.ToListAsync());
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<List<Profile>>> Get(int ProfileId)
        {
            var profile = await _context.Profiles.FirstOrDefaultAsync(x => x.ProfileId == ProfileId);
            if (profile == null)
                return BadRequest("404 profile not found");
            return Ok(profile);
        }

        [HttpPost]
        public async Task<ActionResult<List<Profile>>> AddProfile(Profile profile)
        {
            _context.Profiles.Add(profile);
            await _context.SaveChangesAsync();

            return Ok(await _context.Profiles.ToListAsync());
        }

        [HttpPut]

        public async Task<ActionResult<List<Profile>>> UpdateProfile(Profile Request)
        {
            var dbProfile = await _context.Profiles.FindAsync(Request.ProfileId);
            if (dbProfile == null)
                return NotFound("Hero not found");

            dbProfile.ProfileDes = Request.ProfileDes;

            await _context.SaveChangesAsync();

            return Ok(await _context.Profiles.ToListAsync());
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult<List<Profile>>> Delete(int id)
        {
            var dbProfile = await _context.Profiles.FindAsync(id);
            if (dbProfile == null)
                return BadRequest("404 hero not found");

            _context.Profiles.Remove(dbProfile);
            await _context.SaveChangesAsync();
            return Ok(await _context.Profiles.ToListAsync());
        }
    }
}
