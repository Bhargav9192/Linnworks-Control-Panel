using LinnworksAPI;
using LinnworksAPI.Models.Inventory;
using Newtonsoft.Json;
using Serilog;
using Linnworks.Abstractions;
using Microsoft.Extensions.Caching.Memory;
namespace LinnworksMacro.Orders
{
    public class CreateOrdersFromSnapshotService
    {

        private readonly LinnworksAPI.ApiObjectManager _api;
        private readonly IMemoryCache _cache; // કેશ માટે
        private const string LocationsCacheKey = "Linnworks_Locations_Key";

        public CreateOrdersFromSnapshotService(LinnworksAPI.ApiObjectManager api, IMemoryCache cache)
        {
            _api = api;
            _cache = cache;
        }
        public async Task RunAsync(string userAccount, int validOrders, int invalidOrders, string location)
        {
            await Task.Run(() => Execute(userAccount, validOrders, invalidOrders, location));
        }
        public void Execute(string userAccount, int validOrders, int invalidOrders, string location)
        {
            Console.WriteLine($"Rishvi_create_order_from_snapshot started with {validOrders} valid orders and {invalidOrders} invalid orders");

            //  Load inventory snapshot
            string rootPath = Directory.GetCurrentDirectory();
            string fileName = (string.IsNullOrEmpty(userAccount) || userAccount.Equals("Default", StringComparison.OrdinalIgnoreCase))
                ? "default_snapshot.json"
                : $"{userAccount}_Snapshot.json";

            string snapshotPath = Path.Combine(rootPath, "SnapshotFile", fileName);

            if (!File.Exists(snapshotPath))
            {
                Console.WriteLine("Inventory snapshot file not found");
                Log.Error("Inventory snapshot file not found");
                return;
            }

            var json = File.ReadAllText(snapshotPath);

            var snapshot = JsonConvert.DeserializeObject<InventorySnapshotResponse>(json);

            if (snapshot?.Items == null || snapshot.Items.Count == 0)
            {
                Console.WriteLine("Inventory snapshot empty");
                Log.Error("Inventory snapshot file not found");
                return;
            }

            //  FILTER VALID ITEMS (MOST IMPORTANT FIX)
            var validItems = snapshot.Items
                .Where(x => x.RetailPrice > 0 && x.Available > 0)
                .ToList();

            if (!validItems.Any())
            {
                Console.WriteLine("No valid items with RetailPrice > 0 and Available > 0");
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
                    Console.WriteLine($"Order created successfully. OrderId: {orderIds[0]}");
                    Log.Information($"Order Created Successfully OrderId: {orderIds[0]}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error creating valid order: {ex.Message}");
                }
            }

            //  Loop to create invalid orders
            for (int i = 0; i < invalidOrders; i++)
            {
                //  Pick a random valid item for each order
                var items = validItems[_rnd.Next(validItems.Count)];  // New item selected for each invalid order

                Console.WriteLine($"Creating invalid order for SKU: {items.ItemNumber} - Order #{i + 1}");

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
                    Console.WriteLine($"Order created successfully (Invalid). OrderId: {orderIds[0]}");
                    Log.Information($"Order Created Successfully (Invalid) OrderId: {orderIds[0]}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error creating invalid order: {ex.Message}");
                }
            }

        }

        //  Generate valid customer
        private static readonly Random _rnd = new Random();
        private ChannelAddress GenerateCustomer()
        {
            string[] firstNames = { "Olivia", "Amelia", "Isla", "Ava", "Sophia", "Jack", "Noah", "Leo", "Arthur", "Oscar" };
            string[] lastNames = { "Smith", "Jones", "Taylor", "Brown", "Wilson", "Davies", "Evans", "Thomas", "Johnson", "Roberts" };
            string[] towns = { "London", "Manchester", "Birmingham", "Leeds", "Bristol", "Cardiff", "Edinburgh", "Glasgow", "Liverpool", "Nottingham" };
            string[] counties = { "Greater London", "West Midlands", "Greater Manchester", "West Yorkshire", "Bristol", "South Glamorgan", "Lothian", "Lanarkshire", "Merseyside", "Nottinghamshire" };
            string[] streets = { "High Street", "Station Road", "Church Lane", "Victoria Street", "Main Street", "Park Avenue", "London Road", "Green Lane", "The Crescent", "King Street" };

            string first = firstNames[_rnd.Next(firstNames.Length)];
            string last = lastNames[_rnd.Next(lastNames.Length)];
            string town = towns[_rnd.Next(towns.Length)];
            string county = counties[_rnd.Next(counties.Length)];
            string street = streets[_rnd.Next(streets.Length)];

            int houseNumber = _rnd.Next(1, 201);
            string company = $"{last} Holdings Ltd";
            string postcode = GeneratePostcode();
            string phone = $"+44 7{_rnd.Next(100000000, 999999999)}";
            string email = $"{first.ToLower()}.{last.ToLower()}+sim@rishvi.uk";

            return new ChannelAddress
            {
                FullName = $"{first} {last}",
                Company = company,
                Address1 = $"{houseNumber} {street}",
                Address2 = "",
                Address3 = "",
                Town = town,
                Region = county,
                PostCode = postcode,
                Country = "GB",
                PhoneNumber = phone,
                EmailAddress = email
            };
        }
        private string GeneratePostcode()
        {
            const string letters = "ABCDEFGHJKLMNOPRSTUWYZ";
            const string inwardLetters = "ABDEFGHJLNPQRSTUWXYZ";

            char l1 = letters[_rnd.Next(letters.Length)];
            char l2 = (letters + "0123456789")[_rnd.Next(letters.Length + 10)];
            int digit = _rnd.Next(1, 10);

            int inwardDigit = _rnd.Next(0, 10);
            char i1 = inwardLetters[_rnd.Next(inwardLetters.Length)];
            char i2 = inwardLetters[_rnd.Next(inwardLetters.Length)];

            return $"{l1}{l2}{digit} {inwardDigit}{i1}{i2}";
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
    }
}
