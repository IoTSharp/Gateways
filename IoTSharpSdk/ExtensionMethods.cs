using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace IoTSharp.MqttSdk
{
    public static class ExtensionMethods
    {

        public static byte[] ToBytes<T>(this T t) where T : struct
        {
            int rawsize = Marshal.SizeOf<T>();//得到内存大小
            IntPtr buffer = Marshal.AllocHGlobal(rawsize);//分配内存
            Marshal.StructureToPtr(t, buffer, true);//转换结构
            byte[] rawdatas = new byte[rawsize];
            Marshal.Copy(buffer, rawdatas, 0, rawsize);//拷贝内存
            Marshal.FreeHGlobal(buffer); //释放内存
            return rawdatas;
        }
        public static T  ToStruct<T>(this byte[] rawdatas) where T:struct
        {
            var t = default(T);
            Type anytype = typeof(T);
            int rawsize = Marshal.SizeOf(anytype);
            if (rawsize > rawdatas.Length)
                t = default(T);
            IntPtr buffer = Marshal.AllocHGlobal(rawsize);
            Marshal.Copy(rawdatas, 0, buffer, rawsize);
            object retobj = Marshal.PtrToStructure(buffer, anytype);
            Marshal.FreeHGlobal(buffer);
            t = (T)retobj;
            return t;
        }
        /// <summary>
        /// 把 \0 也剔除掉。 
        /// </summary>
        /// <param name="buffer"></param>
        /// <returns></returns>
        public static string TrimNull(this string  buffer)
        {
            return buffer.Trim('\0');
        }
     
        public static string ToHex(this byte[] data)
        {
            return ToHex(data, "");
        }
        public static string ToHex(this byte[] data, string prefix)
        {
            char[] lookup = new char[] { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'A', 'B', 'C', 'D', 'E', 'F' };
            int i = 0, p = prefix.Length, l = data.Length;
            char[] c = new char[l * 2 + p];
            byte d;
            for (; i < p; ++i) c[i] = prefix[i];
            i = -1;
            --l;
            --p;
            while (i < l)
            {
                d = data[++i];
                c[++p] = lookup[d >> 4];
                c[++p] = lookup[d & 0xF];
            }
            return new string(c, 0, c.Length);
        }
        public static byte[] FromHex(this string str)
        {
            return FromHex(str, 0, 0, 0);
        }
        public static byte[] FromHex(this string str, int offset, int step)
        {
            return FromHex(str, offset, step, 0);
        }
        public static byte[] FromHex(this string str, int offset, int step, int tail)
        {
            byte[] b = new byte[(str.Length - offset - tail + step) / (2 + step)];
            byte c1, c2;
            int l = str.Length - tail;
            int s = step + 1;
            for (int y = 0, x = offset; x < l; ++y, x += s)
            {
                c1 = (byte)str[x];
                if (c1 > 0x60) c1 -= 0x57;
                else if (c1 > 0x40) c1 -= 0x37;
                else c1 -= 0x30;
                c2 = (byte)str[++x];
                if (c2 > 0x60) c2 -= 0x57;
                else if (c2 > 0x40) c2 -= 0x37;
                else c2 -= 0x30;
                b[y] = (byte)((c1 << 4) + c2);
            }
            return b;
        }
    }
}
