using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TronApi
{
    public class UserInventory
    {
        [Key]
        public int InventoryId { get; set; }
        [ForeignKey("User")]
        public int UserId { get; set; }
        public virtual User User { get; set; }
        [ForeignKey("MasterItemsTable")]
        public int ItemId { get; set; }
        public virtual MasterItemsTable Item { get; set; }

        //public int ItemAmount { get; set; }
        //public bool Stackable { get; set; }


        public UserInventory(int userId, int itemId)
        {
            UserId = userId;
            ItemId = itemId;
        }
    }

}
