using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Atlas.Installer;

internal static class WindowsDataProtection
{
    private const int UiForbidden = 0x1;
    public static byte[] Protect(byte[] plaintext) => Transform(plaintext);

    private static byte[] Transform(byte[] value)
    {
        if (value.Length == 0) throw new ArgumentException("DPAPI input vazio.", nameof(value));
        var input = new DataBlob { Size = value.Length, Data = Marshal.AllocHGlobal(value.Length) };
        var output = new DataBlob();
        try
        {
            Marshal.Copy(value, 0, input.Data, value.Length);
            if (!CryptProtectData(ref input, "Atlas catalog key", IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, UiForbidden, ref output))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Falha na proteção DPAPI.");
            var result = new byte[output.Size];
            Marshal.Copy(output.Data, result, 0, output.Size);
            return result;
        }
        finally
        {
            for (var index = 0; index < input.Size; index++) Marshal.WriteByte(input.Data, index, 0);
            Marshal.FreeHGlobal(input.Data);
            if (output.Data != IntPtr.Zero) LocalFree(output.Data);
        }
    }

    [StructLayout(LayoutKind.Sequential)] private struct DataBlob { public int Size; public IntPtr Data; }
    [DllImport("crypt32.dll", CharSet = CharSet.Unicode, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptProtectData(ref DataBlob input, string description, IntPtr optionalEntropy, IntPtr reserved, IntPtr prompt, int flags, ref DataBlob output);
    [DllImport("kernel32.dll")] private static extern IntPtr LocalFree(IntPtr memory);
}
