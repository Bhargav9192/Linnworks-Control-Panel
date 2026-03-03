using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace LinnworksAPI.Models.Inventory
{


    public static class InventorySnapshotLoader
    {
        public static List<InventorySnapshotItem> Load(string path)
        {
            var json = File.ReadAllText(path);
            return JsonConvert.DeserializeObject<List<InventorySnapshotItem>>(json)
               ?? new List<InventorySnapshotItem>();
        }
    }

}
