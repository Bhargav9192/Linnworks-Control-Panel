using System;
using System.Collections.Generic;
using System.Text;

namespace LinnworksAPI.Models.Orders
{
    public static class CustomerGenerator
    {
        private static readonly Random _rnd = new Random();

        public static Dictionary<string, object> Generate()
        {
            string[] first = { "Olivia", "Jack", "Noah", "Ava", "Leo" };
            string[] last = { "Smith", "Brown", "Taylor", "Wilson" };

            string f = first[_rnd.Next(first.Length)];
            string l = last[_rnd.Next(last.Length)];

            return new Dictionary<string, object>
            {
                ["FullName"] = $"{f} {l}",
                ["Address1"] = $"{_rnd.Next(1, 200)} High Street",
                ["Town"] = "London",
                ["Region"] = "Greater London",
                ["PostCode"] = "SW1A 1AA",
                ["Country"] = "GB",
                ["EmailAddress"] = $"{f.ToLower()}.{l.ToLower()}@simulator.test",
                ["PhoneNumber"] = "+447000000000"
            };
        }
    }

}
