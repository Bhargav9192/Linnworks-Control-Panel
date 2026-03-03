using LinnworksAPI;
using LinnworksAPI.Models.Inventory;
using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace LinnworksMacro
{
    public class Rishvi_GetFullStockSnapshot
    {
        private readonly ApiObjectManager _api;
        private string _dataFolder;

        public Rishvi_GetFullStockSnapshot(ApiObjectManager api)
        {
            _api = api;
        }

        public async Task RunAsync(string userAccount, string snapshotFolderPath)
        {
            _dataFolder = snapshotFolderPath;

            userAccount = (userAccount ?? "").Trim();

            string fileName = string.IsNullOrEmpty(userAccount) ||
                              userAccount.Equals("Default", StringComparison.OrdinalIgnoreCase)
                ? "default_snapshot.json"
                : $"{userAccount}_Snapshot.json";

            await FetchAllStockItems(fileName);
        }

        public async Task Execute(string userAccount)
        {
            Log.Information($"Starting stock fetch for User: {userAccount}");

            try
            {
                string rootPath = AppContext.BaseDirectory;
                _dataFolder = Path.Combine(rootPath, "SnapshotFile");

                userAccount = (userAccount ?? "").Trim();

                string fileName = string.IsNullOrEmpty(userAccount) ||
                                  userAccount.Equals("Default", StringComparison.OrdinalIgnoreCase)
                    ? "default_snapshot.json"
                    : $"{userAccount}_Snapshot.json";

                await FetchAllStockItems(fileName);
            }
            catch (Exception ex)
            {
                Log.Error("Fatal error: " + ex);
            }
        }

        public async Task FetchAllStockItems(string fileName)
        {
            const int pageSize = 150; 
            int pageNumber = 1;
            int totalFetched = 0;
            var allStockItems = new List<StockItem>();

            try
            {
                var firstResult = _api.Stock.GetStockItems("", null, pageSize, pageNumber, false, false, false);

                if (firstResult?.Data == null)
                {
                    Log.Warning("No data found in the first request.");
                    return;
                }

                long totalEntries = firstResult.TotalEntries;
                Log.Information($"Total records to fetch: {totalEntries}");

                while (totalFetched < totalEntries)
                {
                    var result = (pageNumber == 1) ? firstResult :
                                 _api.Stock.GetStockItems("", null, pageSize, pageNumber, false, false, false);

                    if (result?.Data == null || result.Data.Count == 0) break;

                    allStockItems.AddRange(result.Data);
                    totalFetched += result.Data.Count;

                    Log.Information($"Progress: {totalFetched} / {totalEntries} (Page: {pageNumber})");

                    if (totalFetched >= totalEntries) break;

                    pageNumber++;
                    
                    // await Task.Delay(50); 
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error during fetch: {ex.Message}");
            }

            // બાકીનું સેવ કરવાનું લોજિક...
            var snapshot = new StockSnapshots
            {
                Count = allStockItems.Count,
                GeneratedAt = DateTime.UtcNow,
                Items = allStockItems
            };

            if (!Directory.Exists(_dataFolder)) Directory.CreateDirectory(_dataFolder);
            string filePath = Path.Combine(_dataFolder, fileName);
            string jsonContent = JsonConvert.SerializeObject(snapshot, Formatting.Indented);
            await File.WriteAllTextAsync(filePath, jsonContent);

            Log.Information($"Success! Total items: {allStockItems.Count} saved to {filePath}");
        }
    }

    public class StockSnapshots
    {
        public int Count { get; set; }
        public DateTime GeneratedAt { get; set; }
        public List<StockItem> Items { get; set; }
    }
}