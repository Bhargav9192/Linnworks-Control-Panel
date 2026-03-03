using System;
using System.Collections.Generic;
using System.Text;

namespace LinnworksAPI.Models.Inventory
{
    public class InventorySnapshotItem
    {
        public Guid StockItemId { get; set; }
        public string ItemNumber { get; set; } = "";
        public string ItemTitle { get; set; } = "";
        public decimal RetailPrice { get; set; }
        public decimal PurchasePrice { get; set; }
        public decimal TaxRate { get; set; }
        public decimal ShippingCost { get; set; }
        public decimal Discount { get; set; }
        public decimal Weight { get; set; }
        public int Available { get; set; }
        public bool IsCompositeParent { get; set; }
    }

}
