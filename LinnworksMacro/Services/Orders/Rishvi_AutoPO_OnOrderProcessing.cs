using Linnworks.Abstractions;
using LinnworksAPI;
using LinnworksMacroHelpers;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace LinnworksMacro.LinnworksTest
{
    public class Rishvi_AutoPO_OnOrderProcesing  
    {
        private readonly LinnworksAPI.ApiObjectManager _api;

        public Rishvi_AutoPO_OnOrderProcesing(LinnworksAPI.ApiObjectManager api)
        {
            _api = api;
        }
        public async Task<(int OrderCount, int POCount)> RunAsync()
        {
            int processedOrders = 0;
            int createdPOs = 0;
            Execute();
            return (processedOrders, createdPOs);
        }
        // ✅ Change these if needed
        private const string CURRENCY = "GBP";
        private const decimal TAX_RATE = 20m;                  // set 0 if you don't want VAT on PO
        private static readonly Random _random = new Random();
        public void Execute()
        {
            Log.Information("=== Auto PO Macro Started ===");

            try
            {
                var ordersToProcess = new Dictionary<Guid, Guid>();
                var successfullyProcessedOrders = new List<OrderDetails>();
                var locations = _api.Inventory.GetStockLocations();

                foreach (var loc in locations)
                {
                    Log.Information($"Fetching open orders for location: {loc.LocationName}");

                    int pageNumber = 1;
                    const int pageSize = 200;

                    var fromDate = DateTime.UtcNow.Date.AddDays(-1);  //this is for get yesterday orders, you can change it as per your requirement
                    //var fromDate = DateTime.UtcNow.Date;
                    var toDate = DateTime.UtcNow.Date;

                    var filter = new FieldsFilter
                    {
                        DateFields = new List<DateFieldFilter>
                        {
                            new DateFieldFilter
                            {
                                FieldCode = FieldCode.GENERAL_INFO_DATE,
                                DateFrom = fromDate,
                                DateTo = toDate,
                                Type = DateTimeFieldFilterType.Range
                            }
                        }
                    };
                    while (true)
                    {
                        var response = _api.Orders.GetOpenOrders(pageSize, pageNumber, filter, null, loc.StockLocationId, null);

                        var openOrders = response?.Data?.ToList() ?? new List<OpenOrder>();

                        if (!openOrders.Any())
                            break;

                        foreach (var order in openOrders)
                        {
                            if (!ordersToProcess.ContainsKey(order.OrderId))
                                ordersToProcess[order.OrderId] = loc.StockLocationId;
                        }

                        if (openOrders.Count < pageSize)
                            break;

                        pageNumber++;
                    }
                }
                if (ordersToProcess.Count == 0)
                {
                    Log.Information("No orders found to process.");
                    return;
                }
                Log.Information($"Orders selected for processing: {ordersToProcess.Count}");
                var allShortageLines = new List<ShortageLine>();
                var supplierCache = new Dictionary<Guid, List<StockItemSupplierStat>>();
                var ordersWithShortage = new HashSet<Guid>();
                var virtualStock = new Dictionary<string, int>();
                foreach (var kvp in ordersToProcess)
                {
                    var orderId = kvp.Key;
                    var currentLocationId = kvp.Value;
                    var order = _api.Orders.GetOrderById(orderId);
                    if (order == null)
                    {
                        Log.Information($"Failed to load order {orderId}");
                        continue;
                    }
                    Log.Information($"------------------------------");
                    Log.Information($"Checking Order: {order.NumOrderId}");

                    bool hasShortage = false;

                    foreach (var item in order.Items)
                    {
                        if (item.ItemId == Guid.Empty || item.Quantity <= 0)
                            continue;

                        bool isComposite = item.CompositeSubItems != null
                                           && item.CompositeSubItems.Any();

                        // ===============================
                        // 🔵 COMPOSITE ITEM HANDLING
                        // ===============================
                        if (isComposite)
                        {
                            Log.Information($"Composite item detected: {item.SKU}");

                            foreach (var child in item.CompositeSubItems)
                            {
                                if (child.StockItemId == Guid.Empty || child.Quantity <= 0)
                                    continue;

                                string childKey = $"{child.StockItemId}_{currentLocationId}";

                                var stockLevels = _api.Stock.GetStockLevel(child.StockItemId);

                                var stockLevel = stockLevels?
                                    .FirstOrDefault(s => s?.Location?.StockLocationId == currentLocationId);

                                if (stockLevel == null)
                                {
                                    Log.Information($"Stock level not found for composite child {child.SKU}");
                                    continue;
                                }

                                int realAvailable = (int)stockLevel.Available;

                                if (!virtualStock.ContainsKey(childKey))
                                {
                                    virtualStock[childKey] = realAvailable;
                                }

                                int available = virtualStock[childKey];
                                int required = child.Quantity;

                                int shortageQty = required > available ? required - available : 0;

                                // Deduct virtual stock
                                virtualStock[childKey] = Math.Max(0, available - required);

                                Log.Information(
                                    $"Child SKU {child.SKU} | Location {currentLocationId} | Required: {required} | Available: {available} | Shortage: {shortageQty}");

                                if (shortageQty <= 0)
                                    continue;

                                hasShortage = true;

                                if (!supplierCache.ContainsKey(child.StockItemId))
                                {
                                    supplierCache[child.StockItemId] =
                                        _api.Inventory.GetStockSupplierStat(child.StockItemId);
                                }

                                var supplierStats = supplierCache[child.StockItemId];

                                var supplierStat = supplierStats?
                                    .FirstOrDefault(x => x.IsDefault)
                                    ?? supplierStats?.FirstOrDefault();

                                if (supplierStat == null || supplierStat.SupplierID == Guid.Empty)
                                {
                                    Log.Information($"No supplier configured for composite child {child.SKU}");
                                    continue;
                                }
                                if (supplierStat.PurchasePrice <= 0)
                                {
                                    Log.Information($"Supplier price is zero for composite child {child.SKU}");
                                    continue;
                                }

                                allShortageLines.Add(new ShortageLine
                                {
                                    SupplierId = supplierStat.SupplierID,
                                    StockItemId = child.StockItemId,
                                    Qty = shortageQty,
                                    LocationId = currentLocationId,
                                    UnitCost = Convert.ToDecimal(supplierStat.PurchasePrice)
                                });

                                ordersWithShortage.Add(order.OrderId);
                            }

                            continue; //  VERY IMPORTANT - Skip parent
                        }
                        // ===============================
                        // 🟢 NORMAL ITEM HANDLING
                        // ===============================

                        string key = $"{item.ItemId}_{currentLocationId}";

                        var stockLevelsNormal = _api.Stock.GetStockLevel(item.ItemId);

                        var stockLevelNormal = stockLevelsNormal?
                            .FirstOrDefault(s => s?.Location?.StockLocationId == currentLocationId);

                        if (stockLevelNormal == null)
                        {
                            Log.Information($"Stock level not found for item {item.SKU}");
                            continue;
                        }

                        int realAvailableNormal = (int)stockLevelNormal.Available;

                        if (!virtualStock.ContainsKey(key))
                        {
                            virtualStock[key] = realAvailableNormal;
                        }

                        int availableNormal = virtualStock[key];
                        int requiredNormal = item.Quantity;

                        int shortageQtyNormal = requiredNormal > availableNormal
                            ? requiredNormal - availableNormal
                            : 0;

                        // Deduct virtual stock
                        virtualStock[key] = Math.Max(0, availableNormal - requiredNormal);

                        Log.Information(
                            $"Item SKU {item.SKU} | Location {currentLocationId} | Required: {requiredNormal} | Available: {availableNormal} | Shortage: {shortageQtyNormal}");

                        if (shortageQtyNormal <= 0)
                            continue;

                        hasShortage = true;

                        if (!supplierCache.ContainsKey(item.ItemId))
                        {
                            supplierCache[item.ItemId] =
                                _api.Inventory.GetStockSupplierStat(item.ItemId);
                        }

                        var supplierStatsNormal = supplierCache[item.ItemId];

                        var supplierStatNormal = supplierStatsNormal?
                            .FirstOrDefault(x => x.IsDefault)
                            ?? supplierStatsNormal?.FirstOrDefault();

                        if (supplierStatNormal == null || supplierStatNormal.SupplierID == Guid.Empty)
                        {
                            Log.Information($"No supplier configured for item {item.SKU}");
                            continue;
                        }

                        if (supplierStatNormal.PurchasePrice <= 0)
                        {
                            Log.Information($"Supplier price is zero for item {item.SKU}");
                            continue;
                        }

                        allShortageLines.Add(new ShortageLine
                        {
                            SupplierId = supplierStatNormal.SupplierID,
                            StockItemId = item.ItemId,
                            LocationId = currentLocationId,
                            Qty = shortageQtyNormal,
                            UnitCost = Convert.ToDecimal(supplierStatNormal.PurchasePrice)
                        });

                        ordersWithShortage.Add(order.OrderId);
                    }
                    // ✅ Process order ONLY if no shortage
                    if (!hasShortage)
                    {
                        var result = _api.Orders.ProcessOrder(order.OrderId, false, currentLocationId, null);
                        if (result != null && result.Processed)
                        {
                            Log.Information($"Order {order.NumOrderId} processed successfully.");

                            successfullyProcessedOrders.Add(order);
                        }
                        else
                        {
                            Log.Information($"Order {order.NumOrderId} failed to process. Error: {result?.Error}");
                        }
                    }
                    else
                    {
                        Log.Information($"Order {order.NumOrderId} has shortage. Will include in consolidated PO.");
                    }
                }
                // =======================================
                // 🎲 RANDOM REFUND LOGIC (ONE PER RUN)
                // =======================================

                if (successfullyProcessedOrders.Count > 1)
                {
                    var randomOrder = successfullyProcessedOrders[
                        _random.Next(successfullyProcessedOrders.Count)];

                    Log.Information($"Random refund selected for Order {randomOrder.NumOrderId}");

                    var validItems = randomOrder.Items
                        .Where(i => i.RowId != Guid.Empty && i.Quantity > 0)
                        .ToList();

                    if (validItems.Any())
                    {
                        var randomItem = validItems[_random.Next(validItems.Count)];

                        int returnQty = 1;

                        decimal refundAmount = Math.Round(
                            Convert.ToDecimal(randomItem.CostIncTax),
                            2,
                            MidpointRounding.AwayFromZero);

                        CreatePartialRefund(
                            randomOrder,
                            randomItem.RowId,
                            returnQty,
                            refundAmount);
                    }
                }
                else
                {
                    Log.Information("Only one or zero orders processed. No random refund triggered.");
                }
                if (allShortageLines.Any())
                {
                    Log.Information("======================================");
                    Log.Information("Creating Consolidated Purchase Orders");
                    Log.Information("======================================");

                    var mergedLines = allShortageLines
                        .GroupBy(x => new { x.SupplierId, x.StockItemId, x.LocationId })
                        .Select(g => new ShortageLine
                        {
                            SupplierId = g.Key.SupplierId,
                            StockItemId = g.Key.StockItemId,
                            LocationId = g.Key.LocationId,
                            Qty = g.Sum(z => z.Qty),
                            UnitCost = g.First().UnitCost
                        })
                        .ToList();

                    foreach (var line in mergedLines)
                    {
                        Log.Information(
                            $"PO Line -> Supplier: {line.SupplierId} | StockItem: {line.StockItemId} | Qty: {line.Qty} | UnitCost: {line.UnitCost}");
                    }
                    CreatePurchaseOrdersBySupplier(mergedLines);

                    Log.Information("Consolidated Purchase Orders created successfully.");
                }
                else
                {
                    Log.Information("No shortages detected. No Purchase Orders created.");
                }
            }
            catch (Exception ex)
            {
                Log.Information("Macro failed: " + ex);
            }
            finally
            {
                Log.Information("=== Auto PO Macro Finished ===");
            }
        }
        private void CreatePartialRefund(OrderDetails order, Guid orderItemRowId, int qty, decimal amount)
        {
            try
            {
                var request = new CreateRefundRequest
                {
                    ChannelInitiated = false,
                    OrderId = order.OrderId,
                    RefundLines = new List<NewRefundLine>
                    {
                        new NewRefundLine
                        {
                            OrderItemRowId = orderItemRowId,
                            RefundedUnit = RefundUnitType.Item,
                            Quantity = qty,
                            Amount = amount,
                            ReasonTag = "AUTO_RANDOM_REFUND",
                            SubReasonTag = "AUTO",
                            IsFreeText = false
                        }
                    }
                };

                var response = _api.ReturnsRefunds.CreateRefund(request);

                if (response == null)
                {
                    Log.Information("Refund response is null");
                    return;
                }

                if (response.Errors != null && response.Errors.Any())
                {
                    foreach (var err in response.Errors)
                    {
                        Log.Information($"Refund Error: {err}");
                    }
                }

                Log.Information($"RefundHeaderId returned: {response.RefundHeaderId}");
                if (response != null && response.RefundHeaderId > 0)
                {
                    _api.ReturnsRefunds.ActionRefund(
                        new ActionRefundRequest
                        {
                            RefundHeaderId = response.RefundHeaderId ?? 0,
                            OrderId = order.OrderId
                        });
                }
            }
            catch (Exception ex)
            {
                Log.Information($"Partial refund failed for Order {order.NumOrderId}: {ex}");
            }
        }
        private (bool Success, Guid OrderItemRowId, int Qty, decimal Amount)
        CreateRandomRMA(OrderDetails order, Guid locationId)
        {
            try
            {
                Log.Information($"Return location being used: {locationId}");
                var validItems = order.Items
                    .Where(i => i.RowId != Guid.Empty && i.Quantity > 0)
                    .ToList();

                if (!validItems.Any())
                    return (false, Guid.Empty, 0, 0);

                // 🎲 Pick random item
                var randomItem = validItems[_random.Next(validItems.Count)];

                // 🎲 Pick random quantity
                int returnQty = _random.Next(1, randomItem.Quantity + 1);

                decimal perUnitAmount = Math.Round(Convert.ToDecimal(randomItem.CostIncTax), 2, MidpointRounding.AwayFromZero);
                decimal refundAmount = Math.Round(perUnitAmount * returnQty, 2, MidpointRounding.AwayFromZero);

                var request = new CreateRMABookingRequest
                {
                    ChannelInitiated = false,
                    OrderId = order.OrderId,
                    ReturnItems = new List<ReturnItem>
                    {
                        new ReturnItem
                        {
                            OrderItemRowId = randomItem.RowId,
                            ReturnItemSKU = randomItem.SKU,
                            ReturnItemTitle = randomItem.Title,
                            ReturnLocation = locationId,
                            ReturnQuantity = returnQty,
                            RefundAmount = refundAmount,
                            ScrapQuantity = 0,
                            ReasonCategory = "AUTO",
                            IsFreeText = true,
                            Reason = "Random auto return",
                            ReasonTag = "AUTO_RANDOM_RETURN"
                        }
                    },
                    ExchangeItems = new List<ExchangeItem>(),
                    ResendItems = new List<ResendItem>(),
                    Reference = $"AUTO_RMA_{DateTime.UtcNow:yyyyMMddHHmmss}"
                };

                try
                {
                    var response = _api.ReturnsRefunds.CreateRMABooking(request);
                    if (response != null && response.RMAHeaderId > 0)
                    {
                        _api.ReturnsRefunds.ActionRMABooking(
                            new ActionRMABookingRequest
                            {
                                RMAHeaderId = response.RMAHeaderId ?? 0,
                                OrderId = order.OrderId
                            });

                        return (true, randomItem.RowId, returnQty, refundAmount);
                    }
                }
                catch (WebException webEx)
                {
                    using (var stream = webEx.Response?.GetResponseStream())
                    using (var reader = new StreamReader(stream))
                    {
                        var responseText = reader.ReadToEnd();
                        Console.WriteLine("HTTP Error Body: " + responseText);
                    }
                }



                return (false, Guid.Empty, 0, 0);
            }
            catch (Exception ex)
            {
                Log.Information($"RMA creation failed for Order {order.NumOrderId}: {ex}");
                return (false, Guid.Empty, 0, 0);
            }
        }
        private void CreatePurchaseOrdersBySupplier(List<ShortageLine> lines)
        {
            var bySupplierAndLocation = lines
            .GroupBy(x => new { x.SupplierId, x.LocationId })
            .ToList();

            foreach (var group in bySupplierAndLocation)
            {
                var supplierId = group.Key.SupplierId;
                var locationId = group.Key.LocationId;

                // Create PO
                var init = new Create_PurchaseOrder_InitialParameter
                {
                    ExternalInvoiceNumber = BuildExternalInvoiceNumber(),
                    fkSupplierId = supplierId,
                    fkLocationId = locationId,
                    PostagePaid = 0,
                    ShippingTaxRate = 0,
                    ConversionRate = 1,
                    QuotedDeliveryDate = DateTime.UtcNow,
                    Currency = CURRENCY,
                    SupplierReferenceNumber = "",
                    UnitAmountTaxIncludedType = 0,
                    DateOfPurchase = DateTime.UtcNow
                };

                var purchaseOrderId = _api.PurchaseOrder.Create_PurchaseOrder_Initial(init);
                Log.Information($"Created PO {purchaseOrderId} for Supplier {supplierId}");

                // Add items to PO
                var bulk = new Modify_PurchaseOrderItems_BulkRequest
                {
                    PurchaseId = purchaseOrderId,
                    ItemsToAdd = new List<AddPurchaseOrderItem>(),
                    ItemsToUpdate = new List<UpdatePurchaseOrderItem>(),
                    ItemsToDelete = new List<Guid>()
                };

                foreach (var item in group)
                {
                    bulk.ItemsToAdd.Add(new AddPurchaseOrderItem
                    {
                        Id = Guid.NewGuid(),
                        StockItemId = item.StockItemId,
                        Cost = item.UnitCost,
                        PackQuantity = item.Qty,
                        PackSize = 1,
                        Qty = item.Qty,
                        TaxRate = TAX_RATE
                    });
                    var lineTotal = item.UnitCost * item.Qty;

                    Log.Information(
                        $"  + PO Line StockItemId={item.StockItemId} Qty={item.Qty} UnitCost={item.UnitCost} LineTotal={lineTotal}");
                }
                try
                {
                    _api.PurchaseOrder.Modify_PurchaseOrderItems_Bulk(bulk);
                }
                catch (WebException webEx)
                {
                    using (var stream = webEx.Response?.GetResponseStream())
                    using (var reader = new StreamReader(stream))
                    {
                        var responseText = reader.ReadToEnd();
                        Log.Information("PO API Error: " + responseText);
                    }
                    throw;
                }
                Log.Information($"Added {bulk.ItemsToAdd.Count} items to PO {purchaseOrderId}");
            }
        }
        private string BuildExternalInvoiceNumber()
        {
            // Example: PO12345678_18022026
            var ticksPart = DateTime.UtcNow.Ticks.ToString();
            var shortTicks = ticksPart.Length > 8 ? ticksPart.Substring(ticksPart.Length - 8) : ticksPart;
            return $"PO{shortTicks}_{DateTime.UtcNow:ddMMyyyy}";
        }
        private class ShortageLine
        {
            public Guid SupplierId { get; set; }
            public Guid StockItemId { get; set; }
            public Guid LocationId { get; set; }
            public int Qty { get; set; }
            public decimal UnitCost { get; set; }
        }
    }
}
