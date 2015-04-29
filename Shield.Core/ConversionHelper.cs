using System;

namespace Shield.Core
{
    public static class ConversionHelper
    {

        public static byte[] ToByteArray(this string hexString)
        {
            Func<char, int> val = c => (c) > 0x39 ? (c - 0x37) : (c - 0x30);
            var arr = new byte[hexString.Length >> 1];

            for (var i = 0; i < hexString.Length >> 1; ++i)
            {
                arr[i] = (byte)((val(hexString[i << 1]) << 4) + (val(hexString[(i << 1) + 1])));
            }

            return arr;
        }
    }
}