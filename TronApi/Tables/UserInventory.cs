using System.ComponentModel.DataAnnotations;

namespace TronApi
{
    public class UserInventory
    {
        [Key]
        public int InventoryId { get; set; }
        public int UserId { get; set; }
        public int ItemId { get; set; }

        //public int ItemAmount { get; set; }
        //public bool Stackable { get; set; }


        public UserInventory(int userId, int itemId)
        {
            UserId = userId;
            ItemId = itemId;
        }
    }

}
