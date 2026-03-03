using Linnworks.Abstractions;
using LinnworksMacroHelpers;
using Serilog.Core;
using System;
using System.Linq;
using System.Reflection;

namespace LinnworksMacro
{
    public class MainScenarioRouter : LinnworksMacroBase
    {
        public void Execute(string scenarioName = "")
        {
            if (string.IsNullOrWhiteSpace(scenarioName))
            {
                Logger.WriteError("Scenario name not provided.");
                return;
            }

            var assembly = Assembly.GetExecutingAssembly();

            var scenarios = assembly
                .GetTypes()
                .Where(t => typeof(IMacroScenario).IsAssignableFrom(t)
                            && !t.IsInterface
                            && !t.IsAbstract)
                .Select(t => (IMacroScenario)Activator.CreateInstance(t))
                .ToList();

            var selected = scenarios
                .FirstOrDefault(s => s.Name.Equals(scenarioName, StringComparison.OrdinalIgnoreCase));

            if (selected == null)
            {
                Logger.WriteError($"Scenario '{scenarioName}' not found.");
                return;
            }

            Logger.WriteInfo($"Running scenario: {selected.Name}");

            selected.RunAsync(new MacroContext()).Wait();

            Logger.WriteInfo("Scenario completed.");
        }
    }
}