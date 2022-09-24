using System.ComponentModel.DataAnnotations;
namespace TronApi
{
    public class MasterItemsTable
    {
        [Key]
        public int ItemId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public string ItemDescription { get; set; } = string.Empty;
        public double OffensiveStat { get; set; }
        public int Value { get; set; }


    }
}
