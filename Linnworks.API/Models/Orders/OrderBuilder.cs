using System;
using System.Collections.Generic;
using System.Text;
using LinnworksAPI.Models.Inventory;

namespace LinnworksAPI.Models.Orders
{
 

    public static class OrderBuilder
    {
        public static Dictionary<string, object> BuildOrder(
            InventorySnapshotItem item,
            int quantity
        )
        {
            var now = DateTimeOffset.UtcNow;

            decimal unitPrice = item.RetailPrice;
            decimal taxRate = item.TaxRate;
            decimal shipping = item.ShippingCost;
            decimal discount = item.Discount;

            decimal subTotal = unitPrice * quantity;
            decimal taxAmount = subTotal * (taxRate / 100m);
            decimal grandTotal = subTotal + taxAmount + shipping - discount;

            string orderRef = $"SIM-{Guid.NewGuid():N}".Substring(0, 12);

            var customer = CustomerGenerator.Generate();

            return new Dictionary<string, object>
            {
                ["Source"] = "Linnworks Simulator",
                ["SubSource"] = "Simulator",
                ["Currency"] = "GBP",
                ["ReferenceNumber"] = orderRef,
                ["ExternalReference"] = orderRef,
                ["ReceivedDate"] = now,
                ["DispatchBy"] = now.AddDays(2),
                ["PaymentStatus"] = "PAID",

                ["Totals"] = new Dictionary<string, object>
                {
                    ["SubTotal"] = subTotal,
                    ["Tax"] = taxAmount,
                    ["GrandTotal"] = grandTotal,
                    ["Shipping"] = shipping,
                    ["Discount"] = discount
                },

                ["OrderItems"] = new[]
                {
                new Dictionary<string, object>
                {
                    ["SKU"] = item.ItemNumber,
                    ["ItemTitle"] = item.ItemTitle,
                    ["StockItemId"] = item.StockItemId,
                    ["Qty"] = quantity,
                    ["PricePerUnit"] = unitPrice,
                    ["TaxRate"] = taxRate,
                    ["CostPerUnit"] = item.PurchasePrice
                }
            },

                ["BillingAddress"] = customer,
                ["DeliveryAddress"] = customer
            };
        }
    }

}
