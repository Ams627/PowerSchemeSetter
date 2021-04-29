using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace PowerSchemeSetter
{
    class Program
    {
        [DllImport("PowrProf.dll")]
        public static extern UInt32 PowerEnumerate(IntPtr RootPowerKey, IntPtr SchemeGuid, IntPtr SubGroupOfPowerSettingGuid, UInt32 AcessFlags, UInt32 Index, ref Guid Buffer, ref UInt32 BufferSize);

        [DllImport("PowrProf.dll")]
        public static extern UInt32 PowerReadFriendlyName(IntPtr RootPowerKey, ref Guid SchemeGuid, IntPtr SubGroupOfPowerSettingGuid, IntPtr PowerSettingGuid, IntPtr Buffer, ref UInt32 BufferSize);

        [DllImport("PowrProf.dll", CharSet = CharSet.Unicode)] static extern UInt32 PowerSetActiveScheme(IntPtr RootPowerKey, [MarshalAs(UnmanagedType.LPStruct)] Guid SchemeGuid);

        public enum AccessFlags : uint
        {
            ACCESS_SCHEME = 16,
            ACCESS_SUBGROUP = 17,
            ACCESS_INDIVIDUAL_SETTING = 18
        }

        private static string ReadFriendlyName(Guid schemeGuid)
        {
            uint sizeName = 1024;
            IntPtr pSizeName = Marshal.AllocHGlobal((int)sizeName);

            string friendlyName;

            try
            {
                PowerReadFriendlyName(IntPtr.Zero, ref schemeGuid, IntPtr.Zero, IntPtr.Zero, pSizeName, ref sizeName);
                friendlyName = Marshal.PtrToStringUni(pSizeName);
            }
            finally
            {
                Marshal.FreeHGlobal(pSizeName);
            }

            return friendlyName;
        }

        public static IEnumerable<Guid> GetAll()
        {
            var schemeGuid = Guid.Empty;

            uint sizeSchemeGuid = (uint)Marshal.SizeOf(typeof(Guid));
            uint schemeIndex = 0;

            while (PowerEnumerate(IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, (uint)AccessFlags.ACCESS_SCHEME, schemeIndex, ref schemeGuid, ref sizeSchemeGuid) == 0)
            {
                yield return schemeGuid;
                schemeIndex++;
            }
        }

        private static void Main(string[] args)
        {
            try
            {
                var guidToPlanName = (from planGuid in GetAll()
                                      let name = ReadFriendlyName(planGuid)
                                      select new { plan=planGuid, name }).ToDictionary(x => x.plan, x=>x.name);
                if (args.Length == 0)
                {
                    foreach (var guid in GetAll())
                    {
                        var name = ReadFriendlyName(guid);
                        Console.WriteLine($"{guid} {name}");
                    }
                }
                else
                {
                    var normalArgs = args.Where(x => x[0] != '-').ToArray();
                    var optionArgs = args.Where(x => x[0] == '-').SelectMany(x => x.Skip(1)).ToHashSet();

                    if (!Guid.TryParse(normalArgs[0], out var guid))
                    {
                        throw new Exception($"{guid} is not a valid power scheme GUID.");
                    }
                    var forever = optionArgs.Contains('f');

                    Console.WriteLine($"forever is {forever}");

                    if (!guidToPlanName.TryGetValue(guid, out var planName))
                    {
                        throw new Exception($"{guid} is not a valid power plan plan GUID");
                    }

                    do
                    {
                        Console.WriteLine($"Setting power plan to {guid} ({planName})");
                        PowerSetActiveScheme(IntPtr.Zero, guid);
                        if (forever) System.Threading.Thread.Sleep(120_000);
                    }
                    while (forever);
                }
            }
            catch (Exception ex)
            {
                var fullname = System.Reflection.Assembly.GetEntryAssembly().Location;
                var progname = Path.GetFileNameWithoutExtension(fullname);
                Console.Error.WriteLine($"{progname} Error: {ex.Message}");
            }

        }
    }
}
