using LinnworksAPI;
using LinnworksAPI.Models.Inventory;
using Newtonsoft.Json;
using System.Net;
using Serilog;

namespace LinnworksMacro.Orders
{
    public class Rishvi_CreateOrder_with_Scenarios
    {

        private static readonly Random _rnd = new Random();
        private readonly LinnworksAPI.ApiObjectManager _api;

        public Rishvi_CreateOrder_with_Scenarios(LinnworksAPI.ApiObjectManager api)
        {
            _api = api;
        }
        public Task RunAsync(string scenario, bool commit, string userAccount, string location)
        {
            Execute(scenario, commit, userAccount, location);
            return Task.CompletedTask;
        }

        public void Execute(string scenario, bool commit, string userAccount, string location)
        {
            Log.Information($"Order Scenarios started | Scenario={scenario} | Commit={commit}");
            if (string.IsNullOrWhiteSpace(location))
                location = "Default";

            string fileName = (string.IsNullOrEmpty(userAccount) || userAccount.Equals("Default", StringComparison.OrdinalIgnoreCase))
            ? "full_stock_snapshot.json"
            : $"{userAccount}_Snapshot.json";

            string snapshotPath = Path.Combine(Directory.GetCurrentDirectory(), "SnapshotFile", fileName);
            if (!File.Exists(snapshotPath))
            {
                Log.Information("Snapshot file not found");
                return;
            }
            var snapshotService = new InventorySnapshotService(snapshotPath);
            var payloadBuilder = new OrderPayloadBuilder();
            var orderService = new LinnworksOrderService(_api);

            var runner = new OrderScenarioRunner(snapshotService, payloadBuilder, orderService);

            runner.Run(scenario, commit, location);
        }
        // ================= RUNNER =================
        private class OrderScenarioRunner
        {
            private readonly InventorySnapshotService _snapshotService;
            private readonly OrderPayloadBuilder _payloadBuilder;
            private readonly LinnworksOrderService _orderService;

            public OrderScenarioRunner(InventorySnapshotService snapshotService, OrderPayloadBuilder payloadBuilder, LinnworksOrderService orderService)
            {
                _snapshotService = snapshotService;
                _payloadBuilder = payloadBuilder;
                _orderService = orderService;
            }
            public void Run(string scenario, bool commit, string location)
            {
                switch (scenario)
                {
                    case "single_in_stock":
                        RunSingleInStock(commit, location);
                        break;
                    case "single_no_stock":
                        RunSingleNoStock(commit, location);
                        break;
                    case "single_composite":
                        RunSingleComposite(commit, location);
                        break;
                    case "single_insufficient_stock":
                        RunSingleInsufficientStock(commit, location);
                        break;
                    case "multi_in_stock":
                        RunMultiInStock(commit, location);
                        break;
                    case "multi_out_stock":
                        RunMultiOutStock(commit, location);
                        break;
                    case "multi_multi_qty":
                        RunMultiWithMultiQty(commit, location);
                        break;
                    case "multi_insufficient_stock":
                        RunMultiInsufficientStock(commit, location);
                        break;
                    case "all":
                        RunSingleInStock(commit, location);
                        RunSingleNoStock(commit, location);
                        RunSingleComposite(commit, location);
                        RunSingleInsufficientStock(commit, location);
                        RunMultiInStock(commit, location);
                        RunMultiOutStock(commit, location);
                        RunMultiWithMultiQty(commit, location);
                        RunMultiInsufficientStock(commit, location);
                        break;

                    default:
                        Log.Information("Unknown scenario");
                        break;
                }
            }
            private void RunSingleInStock(bool commit, string location)
            {
                var item = _snapshotService.GetRandomInStockItem();
                if (item == null)
                {
                    Log.Information("Cannot run scenario: No in-stock items found.");
                    return; //  Jo item na male to ahiya thi j pacha vali jav
                }
                CreateAndSubmit(item, 1, "SingleInStock", commit, location);
            }
            private void RunSingleNoStock(bool commit, string location)
            {
                var item = _snapshotService.GetRandomOutOfStockItem();
                CreateAndSubmit(item, 1, "SingleNoStock", commit, location);
            }
            private void RunSingleComposite(bool commit, string location)
            {
                var item = _snapshotService.GetRandomCompositeItem();
                CreateAndSubmit(item, RandomQuantity(item), "SingleComposite", commit, location);
            }
            private void RunMultiInStock(bool commit, string location)
            {
                var items = _snapshotService.GetMultipleInStockItems(3);

                var payload = _payloadBuilder.Build(items[0], 1, "MultiInStock");

                foreach (var item in items.Skip(1))
                {
                    _payloadBuilder.AppendItem(payload, item, 1);
                }

                PrintPayload(payload);

                if (commit)
                    _orderService.CreateOrder(payload, location);
            }
            private void RunMultiOutStock(bool commit, string location)
            {
                var items = _snapshotService.GetMultipleOutOfStockItems(3);

                var payload = _payloadBuilder.Build(items[0], 1, "MultiOutStock");

                foreach (var item in items.Skip(1))
                {
                    _payloadBuilder.AppendItem(payload, item, 1);
                }

                PrintPayload(payload);

                if (commit)
                    _orderService.CreateOrder(payload, location);
            }
            private void RunMultiInsufficientStock(bool commit, string location)
            {
                var items = _snapshotService.GetMultipleLowStockItems(3);
                if (items == null || items.Count == 0)
                {
                    Log.Information("Scenario Aborted: No low-stock items (qty 1-3) found in snapshot file.");
                    return; // Ahiya thi j pacha vali jav, niche ni line run nahi thay
                }
                // Force qty > available
                var payload = _payloadBuilder.Build(
                    items[0],
                    items[0].Available + 1,
                    "MultiInsufficientStock"
                );

                foreach (var item in items.Skip(1))
                {
                    var forcedQty = item.Available + _rnd.Next(1, 3);
                    _payloadBuilder.AppendItem(payload, item, forcedQty);
                }

                PrintPayload(payload);

                if (commit)
                    _orderService.CreateOrder(payload, location);
            }
            private void RunSingleInsufficientStock(bool commit, string location)
            {
                var item = _snapshotService.GetLowStockItem();
                if (item == null)
                {
                    Log.Information("Scenario Aborted: No low-stock items (qty 1-3) found in snapshot file.");
                    return; // Ahiya thi j pacha vali jav, niche ni line run nahi thay
                }
                // Force insufficient stock
                var qty = item.Available + 1;

                CreateAndSubmit(
                    item,
                    qty,
                    "SingleInsufficientStock",
                    commit,
                    location
                );
            }

            private void RunMultiWithMultiQty(bool commit, string location)
            {
                var items = _snapshotService.GetMultipleItems(3);

                var payload = _payloadBuilder.Build(
                    items[0],
                    RandomQuantity(items[0]),
                    "MultiMultiQty"
                );

                foreach (var item in items.Skip(1))
                {
                    _payloadBuilder.AppendItem(
                        payload,
                        item,
                        RandomQuantity(item)
                    );
                }

                PrintPayload(payload);

                if (commit)
                    _orderService.CreateOrder(payload, location);
            }

            private void CreateAndSubmit(InventorySnapshotItem item, int qty, string subSource, bool commit, string location)
            {

                var payload = _payloadBuilder.Build(
                    item,
                    qty,
                    subSource
                );

                PrintPayload(payload);

                if (commit)
                    _orderService.CreateOrder(payload, location);
            }
            private int RandomQuantity(InventorySnapshotItem item)
            {
                var max = Math.Min(Math.Max(item.Available, 1), 5);
                return _rnd.Next(1, max + 1);
            }
            private void PrintPayload(object payload)
            {
                Log.Information(JsonConvert.SerializeObject(payload, Formatting.Indented));
            }
        }
        // ================= SNAPSHOT SERVICE =================
        private class InventorySnapshotService
        {
            private readonly List<InventorySnapshotItem> _items;

            public InventorySnapshotService(string path)
            {
                var json = File.ReadAllText(path);
                _items = JsonConvert
                    .DeserializeObject<InventorySnapshotResponse>(json)
                    .Items;
            }
            public InventorySnapshotItem GetRandomInStockItem() =>
                _items
                    .Where(i => i.Available > 0)
                    .OrderBy(_ => Guid.NewGuid())
                    .FirstOrDefault();
            public List<InventorySnapshotItem> GetMultipleInStockItems(int count) =>
                _items
                    .Where(i => i.Available > 0)
                    .OrderBy(_ => Guid.NewGuid())
                    .Take(count)
                    .ToList();
            public List<InventorySnapshotItem> GetMultipleLowStockItems(int count) =>
                _items
                    .Where(i => i.Available > 0 && i.Available <= 3)
                    .OrderBy(_ => Guid.NewGuid())
                    .Take(count)
                    .ToList();

            public List<InventorySnapshotItem> GetMultipleOutOfStockItems(int count) =>
                _items
                    .Where(i => i.Available <= 0)
                    .OrderBy(_ => Guid.NewGuid())
                    .Take(count)
                    .ToList();
            public InventorySnapshotItem GetLowStockItem() =>
                _items
                    .Where(i => i.Available > 0 && i.Available <= 3)
                    .OrderBy(_ => Guid.NewGuid())
                    .FirstOrDefault();

            public InventorySnapshotItem GetRandomCompositeItem()
            {
                return _items
                    .Where(i => i.IsCompositeParent && i.Available > 0)
                    .OrderBy(_ => Guid.NewGuid())
                    .FirstOrDefault();
            }
            public InventorySnapshotItem GetRandomOutOfStockItem() =>
                _items.Where(i => i.Available <= 0).OrderBy(_ => Guid.NewGuid()).FirstOrDefault();

            public List<InventorySnapshotItem> GetMultipleItems(int count) =>
                _items.Where(i => i.Available > 0)
                      .OrderBy(_ => Guid.NewGuid())
                      .Take(count)
                      .ToList();
        }
        // ================= PAYLOAD BUILDER =================
        private class OrderPayloadBuilder
        {

            public ChannelOrder Build(InventorySnapshotItem item, int qty, string subSource)
            {
                var order = CreateBaseOrder(subSource);

                order.OrderItems = new List<ChannelOrderItem>
                {
                    BuildLine(item, qty)
                };
                return order;
            }
            public void AppendItem(ChannelOrder order, InventorySnapshotItem item, int qty)
            {
                if (order.OrderItems == null)
                    order.OrderItems = new List<ChannelOrderItem>();

                order.OrderItems.Add(BuildLine(item, qty));
            }
            // ================= HELPERS =================
            private ChannelOrder CreateBaseOrder(string subSource)
            {
                var (customerName, address) = RandomCustomerGenerator.CreateUK();
                return new ChannelOrder
                {
                    Source = "Linnworks Simulator",
                    SubSource = subSource,
                    Site = "Default",
                    Currency = "GBP",
                    ReferenceNumber = $"SW{_rnd.Next(1, 20)} {_rnd.Next(1, 9)}AA",
                    ExternalReference = $"SW{_rnd.Next(1, 20)} {_rnd.Next(1, 9)}AA",
                    ChannelBuyerName = customerName,
                    //  Addresses
                    BillingAddress = address,
                    DeliveryAddress = address,
                    PaymentStatus = PaymentStatus.Paid,
                    PaidOn = DateTime.UtcNow,
                    // REQUIRED dates
                    ReceivedDate = DateTime.UtcNow,
                    DispatchBy = DateTime.UtcNow.AddDays(1),
                    UseChannelTax = false,
                    AutomaticallyLinkBySKU = true,
                    ExtendedProperties = new List<ChannelOrderExtendedProperty>(),
                    Notes = new List<ChannelOrderNote>(),
                    OrderIdentifierTags = new HashSet<string>()
                };
            }
            private ChannelOrderItem BuildLine(InventorySnapshotItem item, int qty)
            {
                var unitPrice = (decimal)item.RetailPrice;
                var taxRate = (decimal)item.TaxRate;

                var taxValue = unitPrice * qty * (taxRate / 100m);

                return new ChannelOrderItem
                {
                    ItemNumber = item.ItemNumber,
                    ChannelSKU = item.ItemNumber,
                    ItemTitle = item.ItemTitle,

                    Qty = qty,

                    PricePerUnit = (double)unitPrice,

                    // Tax behaviour
                    TaxCostInclusive = false,
                    UseChannelTax = false,
                    LineDiscount = 0,
                    IsService = false,

                    Options = new List<ChannelOrderItemOption>(),

                    // ✅ CORRECT TAX OBJECT
                    Taxes = new List<ChannelOrderItemTax>
                    {
                        new ChannelOrderItemTax
                        {
                            TaxType = "VAT",
                            TaxValue = taxValue,
                            IsSellerCollected = true
                        }
                    }
                };
            }
        }
        // ================= SERVICES =================
        private class LinnworksOrderService
        {
            private readonly ApiObjectManager _api;

            public LinnworksOrderService(ApiObjectManager api)
            {
                _api = api;
            }
            public void CreateOrder(ChannelOrder order, string location)
            {
                try
                {
                    var ids = _api.Orders.CreateOrders(new List<ChannelOrder> { order }, location);
                    if (ids != null && ids.Count > 0)
                        Log.Information($"Order created successfully. OrderId = {ids[0]}");
                    else
                        Log.Information("CreateOrders returned no order IDs");
                }
                catch (WebException ex)
                {
                    using var reader = new StreamReader(ex.Response.GetResponseStream());
                    var body = reader.ReadToEnd();
                    Log.Information(body);
                    throw;
                }
            }
        }
        private static class RandomCustomerGenerator
        {
            private static readonly string[] FirstNames =
            {
                "John", "Emma", "Oliver", "Sophia", "Liam", "Ava"
            };

            private static readonly string[] LastNames =
            {
                "Smith", "Patel", "Brown", "Taylor", "Shah", "Jones"
            };

            private static readonly string[] Streets =
            {
                "High Street", "Station Road", "Church Lane", "Park Avenue"
            };
            private static readonly string[] CompanyNames =
            {
                "Acme Ltd","Globex Corporation","Initech","Umbrella Corp","Wayne Enterprises","Stark Industries"
            };
            private static readonly string[] Towns =
            {
                "London", "Manchester", "Birmingham", "Leeds"
            };

            public static (string fullName, ChannelAddress address) CreateUK()
            {
                var first = FirstNames[_rnd.Next(FirstNames.Length)];
                var last = LastNames[_rnd.Next(LastNames.Length)];
                var name = $"{first} {last}";
                var company = _rnd.Next(2) == 0 ? "" : CompanyNames[_rnd.Next(CompanyNames.Length)];
                var address = new ChannelAddress
                {
                    FullName = name,
                    Company = company,
                    Address1 = $"{_rnd.Next(1, 200)} {Streets[_rnd.Next(Streets.Length)]}",
                    Address2 = "",
                    Address3 = "",
                    Town = Towns[_rnd.Next(Towns.Length)],
                    Region = "",
                    PostCode = $"SW{_rnd.Next(1, 20)} {_rnd.Next(1, 9)}AA",
                    // 🔑 THESE TWO ARE CRITICAL
                    Country = "United Kingdom",
                    MatchCountryCode = "GB",
                    MatchCountryName = "United Kingdom",
                    PhoneNumber = $"07{_rnd.Next(100000000, 999999999)}",
                    EmailAddress = $"{first.ToLower()}.{last.ToLower()}@example.com",
                    isEmpty = false
                };

                return (name, address);
            }
        }

        // ================= SNAPSHOT MODELS =================
        private class InventorySnapshotResponse
        {
            public List<InventorySnapshotItem> Items { get; set; }
        }
    }
}
