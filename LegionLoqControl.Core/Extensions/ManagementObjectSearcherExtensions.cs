using System.Collections.Generic;
using System.Linq;
using global::System.Management;
using System.Threading.Tasks;

namespace LegionLoqControl.Core.Extensions
{
    public static class ManagementObjectSearcherExtensions
    {
        public static Task<IEnumerable<ManagementBaseObject>> GetAsync(this ManagementObjectSearcher mos) => Task.Run(() => mos.Get().Cast<ManagementBaseObject>());
    }
}
