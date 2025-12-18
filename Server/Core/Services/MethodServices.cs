using System.Management;
using JiaoLongControl.Server.Core.Models;

namespace JiaoLongControl.Server.Core.Services
{
    public static class MethodServices
    {
        private const int BufferLength = 32;

        private static byte[] MakeMethodParams(MethodType methodType, MethodName methodName)
        {
            var buffer = new byte[BufferLength];
            buffer[1] = (byte)methodType;
            buffer[3] = (byte)methodName;
            return buffer;
        }

        public static T GetValue<T>(MethodName methodName)
        {
            var result = ExecuteMethod(MakeMethodParams(MethodType.Get, methodName));

            if (!result.Item1)
                return GetDefaultValue<T>();

            var data = result.Item2;
            if (typeof(T) == typeof(Tuple<int, int>))
                return (T)(object)new Tuple<int, int>((data[5] << 8) + data[4], (data[7] << 8) + data[6]);
            
            if (typeof(T) == typeof(Tuple<int, int, int>))
                return (T)(object)new Tuple<int, int, int>(data[4], data[5], data[6]);

            return (T)(object)data[4];
        }

        public static bool SetValue(MethodName methodName, object value)
        {
            var data = MakeMethodParams(MethodType.Set, methodName);
            data[4] = Convert.ToByte(value);
            return ExecuteMethod(data).Item1;
        }

        public static bool SetValue(MethodName methodName, byte[] values)
        {
            if (values == null || values.Length + 4 > BufferLength)
                throw new ArgumentException("Invalid value length.");

            var data = MakeMethodParams(MethodType.Set, methodName);
            Array.Copy(values, 0, data, 4, values.Length);
            return ExecuteMethod(data).Item1;
        }

        private static Tuple<bool, byte[]> ExecuteMethod(byte[] inData)
        {
            if (inData.Length != BufferLength)
                return new Tuple<bool, byte[]>(false, null);

            try
            {
                return Task.Run(() =>
                {
                    try
                    {
                        using var mo = new ManagementObject("root\\WMI", "MICommonInterface.InstanceName='ACPI\\PNP0C14\\MIFS_0'", null);
                        var parameters = mo.GetMethodParameters("MiInterface");
                        parameters["InData"] = inData;
                        var output = mo.InvokeMethod("MiInterface", parameters, null)?["OutData"] as byte[];
                        return new Tuple<bool, byte[]>(output != null, output);
                    }
                    catch
                    {
                        return new Tuple<bool, byte[]>(false, null);
                    }
                }).Result;
            }
            catch
            {
                return new Tuple<bool, byte[]>(false, null);
            }
        }

        private static T GetDefaultValue<T>()
        {
            if (typeof(T) == typeof(Tuple<int, int>)) return (T)(object)new Tuple<int, int>(-1, -1);
            if (typeof(T) == typeof(Tuple<int, int, int>)) return (T)(object)new Tuple<int, int, int>(-1, -1, -1);
            if (typeof(T) == typeof(byte)) return (T)(object)byte.MaxValue;
            return default(T)!;
        }
    }
}