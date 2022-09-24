using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace TronApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class InventoryController : ControllerBase
    {
        
        private readonly DataContext _context;

        public InventoryController(DataContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<List<UserInventory>>> Get()
        {
            return Ok(await _context.UserInventories.ToListAsync());
        }
        [HttpGet("UserInventory")]
        public async Task<ActionResult<List<UserInventory>>> GetInventory(int id)
        {
            return Ok(await _context.UserInventories.Where(b => b.UserId.Equals(id)).ToListAsync());
        }


        [HttpGet("{id}")]
        public async Task<ActionResult<List<UserInventory>>> Get(int id)
        {

            var item = await _context.UserInventories.FindAsync(id);
            if (item == null)
            {
                return BadRequest("404 item not found");
            }
                
            else return Ok(item);
        }

        [HttpPost]
        public async Task<ActionResult<List<UserInventory>>> AddOwner(UserInventory item)
        {
            

            _context.UserInventories.Add(item);
            await _context.SaveChangesAsync();

            return Ok(await _context.UserInventories.ToListAsync());
        }



        [HttpDelete("{id}")]
        public async Task<ActionResult<List<UserInventory>>> Delete(int id)
        {
            var dbItem = await _context.UserInventories.FindAsync(id);
            if (dbItem == null)
                return BadRequest("404 item not found");

            _context.UserInventories.Remove(dbItem);
            await _context.SaveChangesAsync();
            return Ok(await _context.UserInventories.ToListAsync());
        }
    }
}
