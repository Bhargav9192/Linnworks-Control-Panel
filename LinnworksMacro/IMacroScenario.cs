using System.Threading.Tasks;

namespace Linnworks.Abstractions
{
    public interface IMacroScenario
    {
        string Name { get; }
        string Description { get; } 
        Task RunAsync(MacroContext context);
    }
}