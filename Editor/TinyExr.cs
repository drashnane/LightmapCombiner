using System;
using System.Runtime.InteropServices;
namespace TinyExr
{

    public class TinyExr
    {
        [DllImport("tinyexr")]
        public static extern int IsEXR(string filename);

        [DllImport("tinyexr", CallingConvention = CallingConvention.Cdecl)]
        public static extern int LoadEXR(ref IntPtr out_rgba, ref int width, ref int height,
                   string filename, ref string err);
        [DllImport("tinyexr", CallingConvention = CallingConvention.Cdecl)]
        public static extern int SaveEXR(IntPtr data, int width, int height,
                   int components, int save_as_fp16,
                   string fileName, ref string err);
    }
}