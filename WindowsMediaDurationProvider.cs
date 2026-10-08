using System.Runtime.InteropServices;

namespace FolderDlnaServer;

internal interface IMediaDurationProvider
{
    /// <summary>
    /// Returns the media duration, or null when Windows does not know it.
    /// </summary>
    TimeSpan? TryGetDuration(string sourcePath);
}

/// <summary>
/// Reads System.Media.Duration from the Windows Shell property system. It is
/// only called by the background warmup, never while answering Browse.
/// </summary>
internal sealed class WindowsMediaDurationProvider : IMediaDurationProvider
{
    private const ushort VtUi8 = 21;
    private static readonly Guid PropertyStoreId = new("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99");
    private static readonly PropertyKey MediaDuration = new(
        new Guid("64440490-4C8B-11D1-8B70-080036B11A03"),
        3);

    public TimeSpan? TryGetDuration(string sourcePath)
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        IPropertyStore? store = null;
        var value = new PropVariant();
        try
        {
            var interfaceId = PropertyStoreId;
            var result = SHGetPropertyStoreFromParsingName(
                sourcePath,
                IntPtr.Zero,
                GetPropertyStoreFlags.Default,
                ref interfaceId,
                out store);
            if (result < 0 || store is null)
            {
                return null;
            }

            var key = MediaDuration;
            if (store.GetValue(ref key, out value) < 0 || value.VariantType != VtUi8)
            {
                return null;
            }

            // System.Media.Duration is expressed in 100 ns units, like TimeSpan ticks.
            var ticks = value.UnsignedValue;
            return ticks is > 0 and <= long.MaxValue
                ? TimeSpan.FromTicks((long)ticks)
                : null;
        }
        catch
        {
            return null;
        }
        finally
        {
            PropVariantClear(ref value);
            if (store is not null)
            {
                Marshal.ReleaseComObject(store);
            }
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHGetPropertyStoreFromParsingName(
        string path,
        IntPtr bindContext,
        GetPropertyStoreFlags flags,
        ref Guid interfaceId,
        [MarshalAs(UnmanagedType.Interface)] out IPropertyStore store);

    [DllImport("ole32.dll")]
    private static extern int PropVariantClear(ref PropVariant value);

    [ComImport]
    [Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        [PreserveSig]
        int GetCount(out uint count);

        [PreserveSig]
        int GetAt(uint index, out PropertyKey key);

        [PreserveSig]
        int GetValue(ref PropertyKey key, out PropVariant value);

        [PreserveSig]
        int SetValue(ref PropertyKey key, ref PropVariant value);

        [PreserveSig]
        int Commit();
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct PropertyKey
    {
        public PropertyKey(Guid formatId, uint propertyId)
        {
            FormatId = formatId;
            PropertyId = propertyId;
        }

        public Guid FormatId;

        public uint PropertyId;
    }

    [StructLayout(LayoutKind.Explicit, Size = 24)]
    private struct PropVariant
    {
        [FieldOffset(0)]
        public ushort VariantType;

        [FieldOffset(8)]
        public ulong UnsignedValue;
    }

    [Flags]
    private enum GetPropertyStoreFlags
    {
        Default = 0
    }
}
