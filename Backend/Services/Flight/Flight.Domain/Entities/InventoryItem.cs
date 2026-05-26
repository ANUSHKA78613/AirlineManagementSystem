using System;
namespace Flight.Domain.Entities
{
    public class InventoryItem
    {
        public int ItemId { get; set; }
        public string SKU { get; set; }
        public string ItemName { get; set; }
        public string Category { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public InventoryItem() { CreatedAt = DateTime.UtcNow; IsActive = true; }
    }
}
