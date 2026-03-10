using Linnworks.Abstractions;
using LinnworksAPI;
using LinnworksAPI.Models.Inventory;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using Serilog;
using static LinnworksMacro.Orders.CreateOrdersFromSnapshotService;
namespace LinnworksMacro.Orders
{
    public class CreateOrdersFromSnapshotService
    {

        private readonly LinnworksAPI.ApiObjectManager _api;
        private readonly IMemoryCache _cache;
        private const string LocationsCacheKey = "Linnworks_Locations_Key";
        private List<PostcodeData> _postcodeList;
        public CreateOrdersFromSnapshotService(LinnworksAPI.ApiObjectManager api, IMemoryCache cache)
        {
            _api = api;
            _cache = cache;
        }
        public Task RunAsync(string userAccount, int validOrders, int invalidOrders, string location)
        {
            Execute(userAccount, validOrders, invalidOrders, location);
            return Task.CompletedTask;
        }
        public void Execute(string userAccount, int validOrders, int invalidOrders, string location)
        {
            Log.Information($"Rishvi_create_order_from_snapshot started with {validOrders} valid orders and {invalidOrders} invalid orders");
            string rootPath = Directory.GetCurrentDirectory();
            
            var postcodePath = Path.Combine(rootPath, "wwwroot", "PostCodes", "uk_postcodes.csv");

            if (File.Exists(postcodePath))
            {
                LoadPostcodes(postcodePath);
            }
            else
            {
                Log.Information("Postcode file not found: {Path}", postcodePath);
                return;
            }
            string fileName = (string.IsNullOrEmpty(userAccount) || userAccount.Equals("Default", StringComparison.OrdinalIgnoreCase))
                ? "default_snapshot.json"
                : $"{userAccount}_Snapshot.json";

            string snapshotPath = Path.Combine(rootPath, "SnapshotFile", fileName);

            if (!File.Exists(snapshotPath))
            {
                Log.Error("Inventory snapshot file not found");
                return;
            }

            var json = File.ReadAllText(snapshotPath);

            var snapshot = JsonConvert.DeserializeObject<InventorySnapshotResponse>(json);

            if (snapshot?.Items == null || snapshot.Items.Count == 0)
            {
                Log.Error("Inventory snapshot file not found");
                return;
            }

            //  FILTER VALID ITEMS (MOST IMPORTANT FIX)
            var validItems = snapshot.Items
                .Where(x => x.RetailPrice > 0 && x.Available > 0)
                .ToList();

            if (!validItems.Any())
            {
                Log.Information("No valid items with RetailPrice > 0 and Available > 0");
                return;
            }

            //  Loop to create valid orders
            for (int i = 0; i < validOrders; i++)
            {
                //  Pick a random valid item for each order
                var items = validItems[_rnd.Next(validItems.Count)];  // New item selected for each order

                Console.WriteLine($"Creating valid order for SKU: {items.ItemNumber} - Order #{i + 1}");

                var orderRef = $"SIM-{items.ItemNumber}-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{i + 1}"; // Unique reference for each order

                var customer = GenerateCustomer(); // Valid customer data

                //  Build ChannelOrder (SDK-safe)
                var order = new ChannelOrder
                {
                    Source = "Linnworks Simulator",
                    SubSource = "SnapshotMacro",
                    ChannelBuyerName = customer.FullName,
                    Site = "Default",
                    Currency = "GBP",
                    ReferenceNumber = orderRef,
                    ExternalReference = orderRef,
                    AutomaticallyLinkBySKU = true,
                    ReceivedDate = DateTime.UtcNow,
                    DispatchBy = DateTime.UtcNow.AddDays(2),
                    PaymentStatus = PaymentStatus.Paid,
                    PaidOn = DateTime.UtcNow,
                    BillingAddress = customer,  // Valid customer billing address
                    DeliveryAddress = customer,  // Valid customer delivery address
                    PostalServiceName = "Default",
                    OrderItems = new List<ChannelOrderItem>
                    {
                        new ChannelOrderItem
                        {
                            ItemNumber = items.ItemNumber,
                            ChannelSKU = items.ItemNumber,
                            ItemTitle = items.ItemTitle,
                            Qty = 1,
                            PricePerUnit = (double)items.RetailPrice,
                            TaxRate = (double)items.TaxRate,
                            UseChannelTax = false,
                            TaxCostInclusive = false,
                            IsService = false
                        }
                    },
                    OrderState = OrderState.None
                };
                // Create valid order
                try
                {
                    var orderIds = _api.Orders.CreateOrders(new List<ChannelOrder> { order }, location);

                    var Norder = _api.Orders.GetOrderById(orderIds[0]);

                    Log.Information($"Order Created Successfully OrderId: {Norder.NumOrderId}");
                }
                catch (Exception ex)
                {
                    Log.Information(ex, $"Error creating valid order");
                }
            }

            //  Loop to create invalid orders
            for (int i = 0; i < invalidOrders; i++)
            {
                //  Pick a random valid item for each order
                var items = validItems[_rnd.Next(validItems.Count)];  // New item selected for each invalid order

                Log.Information($"Creating invalid order for SKU: {items.ItemNumber} - Order #{i + 1}");

                var orderRef = $"SIM-{items.ItemNumber}-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{i + 1}"; // Unique reference for each order

                var customer = GenerateInvalidCustomer(); // Generate invalid customer data (missing details)

                //  Build ChannelOrder (SDK-safe)
                var order = new ChannelOrder
                {
                    Source = "Linnworks Simulator",
                    SubSource = "SnapshotMacro",
                    ChannelBuyerName = customer.FullName,  // Invalid name if missing
                    Site = "Default",
                    Currency = "GBP",
                    ReferenceNumber = orderRef,
                    ExternalReference = orderRef,
                    AutomaticallyLinkBySKU = true,
                    ReceivedDate = DateTime.UtcNow,
                    DispatchBy = DateTime.UtcNow.AddDays(2),
                    PaymentStatus = PaymentStatus.Paid,
                    PaidOn = DateTime.UtcNow,
                    BillingAddress = customer,  // Invalid address if missing
                    DeliveryAddress = customer,  // Invalid address if missing
                    PostalServiceName = "Default",
                    OrderItems = new List<ChannelOrderItem>
                    {
                        new ChannelOrderItem
                        {
                            ItemNumber = items.ItemNumber,
                            ChannelSKU = items.ItemNumber,
                            ItemTitle = items.ItemTitle,
                            Qty = 1,
                            PricePerUnit = (double)items.RetailPrice,
                            TaxRate = (double)items.TaxRate,
                            UseChannelTax = false,
                            TaxCostInclusive = false,
                            IsService = false
                        }
                    },
                    OrderState = OrderState.None
                };

                // Create invalid order
                try
                {
                    var orderIds = _api.Orders.CreateOrders(new List<ChannelOrder> { order }, location);
                    var Norder = _api.Orders.GetOrderById(orderIds[0]);
                    Log.Information($"Order Created Successfully (Invalid) OrderId: {Norder.NumOrderId}");
                }
                catch (Exception ex)
                {
                    Log.Information($"Error creating invalid order: {ex.Message}");
                }
            }

        }

        //  Generate valid customer
        private static readonly Random _rnd = new Random();
        private static readonly string[] Address2Options =
        {
            "Flat 1", "Flat 2", "Flat 3A", "Flat 4B",
            "Apartment 12", "Suite 5", "Unit 8",
            "Floor 2", "Room 15"
        };

        private static readonly string[] Address3Options =
        {
            "Riverside Court",
            "Victoria Building",
            "The Residency",
            "Central Plaza",
            "Park View Apartments",
            "Kings Tower",
            "City Heights",
            "Greenwood Estate"
        };
        private ChannelAddress GenerateCustomer()
        {
            string[] firstNames = { "Olivia", "Amelia", "Isla", "Ava", "Sophia", "Jack", "Noah", "Leo", "Arthur", "Oscar" };
            string[] lastNames = { "Smith", "Jones", "Taylor", "Brown", "Wilson", "Davies", "Evans", "Thomas", "Johnson", "Roberts" };           
            string[] streets = { "High Street", "Station Road", "Church Lane", "Victoria Street", "Main Street", "Park Avenue", "London Road", "Green Lane", "The Crescent", "King Street" };

            string first = firstNames[_rnd.Next(firstNames.Length)];
            string last = lastNames[_rnd.Next(lastNames.Length)];
            string street = streets[_rnd.Next(streets.Length)];

            int houseNumber = _rnd.Next(1, 201);
            string company = $"{last} Holdings Ltd";
            var postcodeData = GetRandomPostcode();
            string phone = $"+44 7{_rnd.Next(100000000, 999999999)}";
            string email = $"{first.ToLower()}.{last.ToLower()}+sim@rishvi.uk";

            return new ChannelAddress
            {
                FullName = $"{first} {last}",
                Company = company,
                Address1 = $"{houseNumber} {street}",
                Address2 = Address2Options[_rnd.Next(Address2Options.Length)],
                Address3 = Address3Options[_rnd.Next(Address3Options.Length)],
                Town = postcodeData.Town,
                Region = postcodeData.Region,
                PostCode = postcodeData.Postcode,
                Country = postcodeData.Country,
                PhoneNumber = phone,
                EmailAddress = email
            };
        }
        private PostcodeData GetRandomPostcode()
        {
            if (_postcodeList == null || !_postcodeList.Any())
                throw new Exception("Postcode list is empty.");

            return _postcodeList[_rnd.Next(_postcodeList.Count)];
        }
        private void LoadPostcodes(string path)
        {
            if (!File.Exists(path))
                throw new Exception($"Postcode file not found: {path}");

            _postcodeList = File.ReadAllLines(path)
                .Skip(1)
                .Select(line =>
                {
                    var cols = line.Split(new[] { ',', '\t' });

                    if (cols.Length < 7)
                        return null;

                    return new PostcodeData
                    {
                        Postcode = Clean(cols[0]),
                        Town = Clean(cols[5]),
                        Region = Clean(cols[6]),
                        Country = Clean(cols[7])
                    };
                })
                .Where(x => x != null)
                .ToList();
        }
        private string Clean(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            return value.Trim().Trim('"');
        }
        //  Generate invalid customer (missing details like address, name, etc.)
        private ChannelAddress GenerateInvalidCustomer()
        {
            var firstNames = new[] { "Olivia", "Amelia", "Jack" };  // Simpler set for invalid customer
            var lastNames = new[] { "Smith", "Jones" };
            var customer = new ChannelAddress
            {
                FullName = $"{firstNames[_rnd.Next(firstNames.Length)]} {lastNames[_rnd.Next(lastNames.Length)]}",
                Company = "",  // Missing company name
                Address1 = "",  // Missing address
                Town = "",      // Missing town
                Country = "GB", // Valid country but other fields may be missing
                PhoneNumber = "", // Missing phone
                EmailAddress = ""  // Missing email
            };
            return customer;
        }
        public List<string> GetLinnworksLocations(string userAccount)
        {
            string dynamicCacheKey = $"{LocationsCacheKey}_{userAccount}";

            if (!_cache.TryGetValue(dynamicCacheKey, out List<string> cachedLocations))
            {
                try
                {
                    var locations = _api.Inventory.GetStockLocations();
                    cachedLocations = locations.Select(l => l.LocationName).ToList();

                    var cacheEntryOptions = new MemoryCacheEntryOptions()
                        .SetAbsoluteExpiration(TimeSpan.FromMinutes(30));

                    _cache.Set(dynamicCacheKey, cachedLocations, cacheEntryOptions);
                }
                catch (Exception ex)
                {
                    return new List<string> { "Default" };
                }
            }
            return cachedLocations;
        }
        // Snapshot wrapper
        public class InventorySnapshotResponse
        {
            public int Count { get; set; }
            public DateTime GeneratedAt { get; set; }
            public List<InventorySnapshotItem> Items { get; set; }
        }
        public class PostcodeData
        {
            public string Postcode { get; set; }
            public string Town { get; set; }
            public string Region { get; set; }
            public string Country { get; set; }
        }
    }
}
