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
            // Find the item in the MasterItemsTable
            var masterItem = await _context.MasterItemsTables.FindAsync(item.ItemId);
            if (masterItem == null)
            {
                return BadRequest("Item not found in MasterItemsTable");
            }

            // Find the user's stats
            var userStats = await _context.UsersStats.FirstOrDefaultAsync(u => u.UserId == item.UserId);
            if (userStats == null)
            {
                return BadRequest("User stats not found");
            }

            // Check if the user has enough money
            if (userStats.Money < masterItem.Value)
            {
                return BadRequest("Not enough money");
            }

            // Subtract the value of the item from the user's money
            userStats.Money -= masterItem.Value;

            // Add the item to the user's inventory
            _context.UserInventories.Add(item);

            // Save the changes to the database
            await _context.SaveChangesAsync();

            // Return the updated list of UserInventories
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
