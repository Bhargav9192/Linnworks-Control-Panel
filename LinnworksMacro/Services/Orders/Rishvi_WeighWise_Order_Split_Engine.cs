using Linnworks.Abstractions;
using LinnworksAPI;
using LinnworksMacroHelpers;
using Serilog;
using System.Threading.Tasks;
using System.Linq;

namespace LinnworksMacro.Orders
{
    public class Rishvi_WeighWise_Order_Split_Engine  
    {
        private readonly LinnworksAPI.ApiObjectManager _api;

        public Rishvi_WeighWise_Order_Split_Engine(LinnworksAPI.ApiObjectManager api)
        {
            _api = api;
        }
        public async Task<int> RunAsync(int[] numOrderIds, double maxAllowedKg)
        {
            return await Task.Run(() => Execute(numOrderIds, maxAllowedKg));
        }

        //double maxAllowedKg = 20.0;

        public int Execute(int[] numOrderIds, double maxAllowedKg)
        {
            int splitCount = 0;
            foreach (var numId in numOrderIds)
            {
                var order = _api.Orders.GetOrderDetailsByNumOrderId(numId);
                if (order == null) continue;

                // ProcessingOrder boolean return karshe
                if (ProcessingOrder(order, maxAllowedKg))
                {
                    splitCount++;
                }
            }
            return splitCount;
        }
        public bool ProcessingOrder(OrderDetails  order, double maxAllowedKg)
        {
            if (order == null)
            {
                Log.Information($"Order not found: {order.NumOrderId}");
                return false;
            }

            try
            {
                // 2. Ask Linnworks to calculate packaging (this will calculate item weights, packaging and suggest splits)
                var calcRequest = new GetOrderPackagingCalculationRequest
                {
                    pkOrderIds = new Guid[] { order.OrderId },
                    Recalculate = true,
                    SaveRecalculation = false
                };

                var calc = _api.Orders.GetOrderPackagingCalculation(calcRequest)?.FirstOrDefault();

                if (calc == null)
                {
                    Log.Information($"Failed to get packaging calculation for order {order.OrderId}");
                    return false;
                }
                // Prefer package type suggested by calculation; fall back to maximum available packaging capacity for the calculated group.
                double? capacityKg = null;

                if (calc.fkPackagingTypeId != Guid.Empty)
                {
                    // Look up packaging groups and types to find the selected type capacity
                    var groups = _api.Orders.GetPackagingGroups();
                    var selected = groups?
                        .SelectMany(g => g.PackageTypes ?? new List<PackageType>())
                        .FirstOrDefault(t => t.PackageTypeId == calc.fkPackagingTypeId);

                    if (selected != null)
                    {
                        if (selected.PackagingCapacity > 0)
                            capacityKg = selected.PackagingCapacity;        // already KG
                        else if (selected.ToGramms > 0)
                            capacityKg = selected.ToGramms / 1000.0;        // grams → KG
                    }

                    if (selected != null)
                    {
                        Log.Information(
                            $"PackageTitle={selected.PackageTitle}, CapacityKg={capacityKg}, RawCapacity={selected.PackagingCapacity}, ToGramms={selected.ToGramms}"
                        );
                    }
                }
                if (capacityKg == null && calc.fkPackagingGroupId != Guid.Empty)
                {
                    var groups = _api.Orders.GetPackagingGroups();
                    var group = groups?.FirstOrDefault(g => g.PackageCategoryID == calc.fkPackagingGroupId);
                    if (group != null && group.PackageTypes?.Any() == true)
                    {
                        // Take the maximum 'ToGramms' or PackagingCapacity from available package types
                        var maxCapacityGram = group.PackageTypes.Max(t => Math.Max(t.ToGramms, t.PackagingCapacity));
                        if (maxCapacityGram > 0)
                            capacityKg = maxCapacityGram / 1000.0;
                    }
                }
                // 4. Use calculated item weight (items only) and total weight (items + packaging)
                var itemsWeightKg = (double)calc.ItemWeight; // calc.ItemWeight is Decimal
                var totalWeightKg = (double)calc.TotalWeight;
                var splitGroups = new List<List<OrderItem>>();
                if (capacityKg.HasValue)
                {
                    Log.Information($"Determined packaging capacity: {capacityKg.Value} kg");
                }

                if (totalWeightKg > maxAllowedKg)
                {
                    Log.Information($"Order {order.NumOrderId} exceeds courier limit ({totalWeightKg}kg > {maxAllowedKg}kg). Splitting required.");
                    double packagingWeightKg = totalWeightKg - itemsWeightKg;
                    if (packagingWeightKg < 0) packagingWeightKg = 0;
                    double usableItemCapacityKg = maxAllowedKg - packagingWeightKg;

                    if (usableItemCapacityKg <= 0)
                    {
                        Log.Information(
                            $"Order {order.NumOrderId}: packaging weight {packagingWeightKg}kg exceeds courier limit {maxAllowedKg}kg. Manual handling required."
                        );
                        return false;
                    }
                    var bins = new List<List<OrderItem>>();
                    var currentBin = new List<OrderItem>();
                    double currentWeight = 0;

                    foreach (var item in order.Items)
                    {
                        // Guard: indivisible unit overweight
                        if (item.Weight > usableItemCapacityKg)
                        {
                            Log.Information($"Order {order.NumOrderId}: SKU {item.SKU} unit weight {item.Weight}kg cannot fit after packaging (usable {usableItemCapacityKg}kg).");
                            return false;
                        }
                        int remainingQty = item.Quantity;

                        while (remainingQty > 0)
                        {
                            double remainingCapacity = usableItemCapacityKg - currentWeight;
                            // If current bin cannot fit even 1 unit, close it and start new bin
                            if (remainingCapacity < item.Weight)
                            {
                                if (currentBin.Count > 0) bins.Add(currentBin);
                                currentBin = new List<OrderItem>();
                                currentWeight = 0;
                                continue;
                            }

                            // Fit as many units as possible into current bin
                            int qtyFit = (int)Math.Floor(remainingCapacity / item.Weight);
                            if (qtyFit <= 0) qtyFit = 1;
                            if (qtyFit > remainingQty) qtyFit = remainingQty;

                            // Clone a "partial" order item for this bin
                            var partial = new OrderItem
                            {
                                RowId = item.RowId,
                                SKU = item.SKU,
                                Quantity = qtyFit,
                                Weight = item.Weight,
                                UnitCost = item.UnitCost
                            };

                            currentBin.Add(partial);
                            currentWeight += qtyFit * item.Weight;
                            remainingQty -= qtyFit;
                        }
                    }

                    if (currentBin.Count > 0)
                        bins.Add(currentBin);

                    var orderSplits = bins
                    .Skip(1)
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
                    if (bins.Count < 2)
                    {
                        Log.Information($"Order {order.NumOrderId}: No split required.");
                        return false;
                    }
                    if (bins.Any(b => (b.Sum(x => x.Weight * x.Quantity) + packagingWeightKg) > maxAllowedKg + 0.0001))
                    {
                        Log.Information($"Order {order.NumOrderId}: internal error – overweight bin produced. Split aborted.");
                        return false;
                    }

                    // Ask for suggested split packaging
                    var newOrders = _api.Orders.SplitOrder(order.OrderId, orderSplits.ToArray(), "AUTO_SPLIT", order.FulfilmentLocationId, false);
                    Log.Information(
                        $"[AUTO_SPLIT] SplitOrder API SUCCESS. Returned Orders = {newOrders.Count}"
                    );
                    if (newOrders != null && newOrders.Count > 0)
                    {
                        return true;
                    }
                }
                else
                {
                    Log.Information($"Order {order.NumOrderId} is within courier weight limit.");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Log.Information($"Error processing order {order.NumOrderId}: {ex.Message}");
                return false;
            }
            return false;
        }
    }
}
