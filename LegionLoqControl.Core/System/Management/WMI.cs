using System;
using System.Collections.Generic;
using System.Linq;
using global::System.Management;
using System.Threading.Tasks;
using LegionLoqControl.Core.Extensions;

namespace LegionLoqControl.Core.System.Management
{
    public static partial class WMI
    {
        private static async Task<bool> ExistsAsync(string scope, FormattableString query)
        {
            try
            {
                var mos = new ManagementObjectSearcher(scope, query.ToString());
                var managementObjects = await mos.GetAsync().ConfigureAwait(false);
                return managementObjects.Any();
            }
            catch
            {
                return false;
            }
        }

        private static async Task CallAsync(string scope, FormattableString query, string methodName, Dictionary<string, object> methodParams)
        {
            try
            {
                var mos = new ManagementObjectSearcher(scope, query.ToString());
                var managementObjects = await mos.GetAsync().ConfigureAwait(false);
                var managementObject = managementObjects.FirstOrDefault() ?? throw new InvalidOperationException("No results in query");

                var mo = (ManagementObject)managementObject;
                
                // Always use ManagementClass to get parameters to avoid compilation/runtime issues with instances
                using var mc = new ManagementClass(scope, mo.ClassPath.ClassName, null);
                var methodParamsObject = mc.GetMethodParameters(methodName);

                foreach (var pair in methodParams)
                    methodParamsObject[pair.Key] = pair.Value;

                mo.InvokeMethod(methodName, methodParamsObject, new InvokeMethodOptions());
            }
            catch (ManagementException ex)
            {
                throw new ManagementException($"Call failed: {ex.Message} [scope={scope}, query={query}, methodName={methodName}]", ex);
            }
        }

        private static async Task<T> CallAsync<T>(string scope, FormattableString query, string methodName, Dictionary<string, object> methodParams, Func<PropertyDataCollection, T> converter)
        {
            try
            {
                var mos = new ManagementObjectSearcher(scope, query.ToString());
                var managementObjects = await mos.GetAsync().ConfigureAwait(false);
                var managementObject = managementObjects.FirstOrDefault() ?? throw new InvalidOperationException("No results in query");

                var mo = (ManagementObject)managementObject;
                
                using var mc = new ManagementClass(scope, mo.ClassPath.ClassName, null);
                var methodParamsObject = mc.GetMethodParameters(methodName);

                foreach (var pair in methodParams)
                    methodParamsObject[pair.Key] = pair.Value;

                var resultProperties = mo.InvokeMethod(methodName, methodParamsObject, new InvokeMethodOptions());
                var result = converter(resultProperties.Properties);
                return result;
            }
            catch (ManagementException ex)
            {
                throw new ManagementException($"Call failed: {ex.Message}. [scope={scope}, query={query}, methodName={methodName}]", ex);
            }
        }
    }
}
