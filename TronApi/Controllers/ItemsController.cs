using Microsoft.AspNetCore.Mvc;

namespace TronApi.Controllers
{

    [Route("api/items")]
    [ApiController]
    public class ItemsController : ControllerBase
    {

        private readonly DataContext _context;

        public ItemsController(DataContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<List<MasterItemsTable>>> Get()
        {
            return Ok(await _context.MasterItemsTables.ToListAsync());
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<List<MasterItemsTable>>> Get(int id)
        {
            var item = await _context.MasterItemsTables.FindAsync(id);
            if (item == null)
                return BadRequest("404 item not found");
            return Ok(item);
        }

        [HttpPost]
        public async Task<ActionResult<List<MasterItemsTable>>> AddItem(MasterItemsTable item)
        {
            _context.MasterItemsTables.Add(item);
            await _context.SaveChangesAsync();

            return Ok(await _context.MasterItemsTables.ToListAsync());
        }

        [HttpPut]

        public async Task<ActionResult<List<MasterItemsTable>>> UpdateItem(MasterItemsTable Request)
        {
            var dbItem = await _context.MasterItemsTables.FindAsync(Request.ItemId);
            if (dbItem == null)
                return NotFound("item not found");

            dbItem.ItemName = Request.ItemName;
            dbItem.ItemDescription = Request.ItemDescription;
            dbItem.OffensiveStat = Request.OffensiveStat;
            dbItem.Value = Request.Value;

            await _context.SaveChangesAsync();

            return Ok(await _context.MasterItemsTables.ToListAsync());
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult<List<MasterItemsTable>>> Delete(int id)
        {
            var dbItem = await _context.MasterItemsTables.FindAsync(id);
            if (dbItem == null)
                return BadRequest("404 item not found");

            _context.MasterItemsTables.Remove(dbItem);
            await _context.SaveChangesAsync();
            return Ok(await _context.MasterItemsTables.ToListAsync());
        }
    }
}
