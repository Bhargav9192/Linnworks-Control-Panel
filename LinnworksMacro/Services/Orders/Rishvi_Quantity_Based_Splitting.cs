using Linnworks.Abstractions;
using LinnworksAPI;
using LinnworksMacroHelpers;
using Serilog;
using System;
using System.Collections.Generic;
using System.Text;

namespace LinnworksMacro.Orders
{
    public class Rishvi_Quantity_Based_Splitting 
    {
        private readonly LinnworksAPI.ApiObjectManager _api;

        public Rishvi_Quantity_Based_Splitting(LinnworksAPI.ApiObjectManager api)
        {
            _api = api;
        }
        public async Task<int> RunAsync(int[] numOrderIds, int QuantityThreshold)
        {
            // Execute method jo int return karti hoy to aa mujab lakho
            return await Task.Run(() => Execute(numOrderIds, QuantityThreshold));
        }
        public int Execute(int[] numOrderIds,int QuantityThreshold)
        {
            int totalSplitDone = 0;
            foreach (var numId in numOrderIds)
            {
                var order = _api.Orders.GetOrderDetailsByNumOrderId(numId);

                if (order == null)
                {
                    Log.Information($"Order not found for NumOrderId: {numId}");
                    continue;
                }
                // ProcessOrder have boolean return karshe
                bool isSplit = ProcessOrder(order, QuantityThreshold);
                if (isSplit)
                {
                    totalSplitDone++;
                }
            }
            return totalSplitDone; // Controller ne total count malshe
        }
        

        private bool ProcessOrder(OrderDetails order, int QuantityThreshold)
        {
            if (order == null) return false;

            int totalQuantity = order.Items.Sum(i => i.Quantity);
            Log.Information($"Order {order.NumOrderId}: total item quantity = {totalQuantity}");

            // Jo threshold karta ochi qty hoy to split na thay (return false)
            if (totalQuantity <= QuantityThreshold)
            {
                Log.Information($"Order {order.NumOrderId}: No split required.");
                return false;
            }

            Log.Information(
                $"Order {order.NumOrderId}: exceeds quantity threshold ({totalQuantity} > {QuantityThreshold}). Splitting required."
            );

            var bins = new List<List<OrderItem>>();
            var currentBin = new List<OrderItem>();
            int currentQty = 0;

            foreach (var item in order.Items)
            {
                int remainingQty = item.Quantity;

                while (remainingQty > 0)
                {
                    int remainingCapacity = QuantityThreshold - currentQty;

                    if (remainingCapacity == 0)
                    {
                        bins.Add(currentBin);
                        currentBin = new List<OrderItem>();
                        currentQty = 0;
                        continue;
                    }

                    int qtyToAdd = Math.Min(remainingQty, remainingCapacity);

                    currentBin.Add(new OrderItem
                    {
                        RowId = item.RowId,
                        SKU = item.SKU,
                        Quantity = qtyToAdd,
                        Weight = item.Weight,
                        UnitCost = item.UnitCost
                    });

                    currentQty += qtyToAdd;
                    remainingQty -= qtyToAdd;
                }
            }

            if (currentBin.Count > 0)
                bins.Add(currentBin);

            if (bins.Count < 2)
            {
                Log.Information($"Order {order.NumOrderId}: no valid quantity split produced.");
                return false;
            }

            var orderSplits = bins
                .Skip(1) // keep first bin in original order
                .Select(bin => new OrderSplit
                {
                    Items = bin.Select(i => new OrderSplitOutItem
                    {
                        RowId = i.RowId,
                        Quantity = i.Quantity,
                        Weight = i.Weight,
                        UnitCost = i.UnitCost
                    }).ToList(),
                    PostalServiceId = order.ShippingInfo.PostalServiceId
                })
                .ToArray();

            var newOrders = _api.Orders.SplitOrder(
                order.OrderId,
                orderSplits,
                "AUTO_SPLIT_QTY",
                order.FulfilmentLocationId,
                false
            );
            if (newOrders != null && newOrders.Count > 0)
            {
                Log.Information($"[AUTO_SPLIT_QTY] Split successful for Order {order.NumOrderId}");
                return true; // Kharekhar split thayu!
            }
            return false;
        }
    }
}
