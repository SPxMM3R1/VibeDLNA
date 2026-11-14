using System.Runtime.InteropServices;

namespace FolderDlnaServer;

/// <summary>
/// Lector de propiedades del Shell de Windows (IPropertyStore) para un archivo. Solo lee
/// metadatos que Windows ya conoce; nunca se llama desde las respuestas Browse, únicamente
/// desde el calentamiento en segundo plano.
/// </summary>
internal sealed class ShellPropertyReader : IDisposable
{
    private const ushort VtUi4 = 19;
    private const ushort VtUi8 = 21;
    private const ushort VtLpwstr = 31;
    private static readonly Guid PropertyStoreId = new("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99");

    private IPropertyStore? _store;

    private ShellPropertyReader(IPropertyStore store)
    {
        _store = store;
    }

    /// <summary>
    /// Abre las propiedades del archivo, o devuelve null si Windows no puede leerlo.
    /// </summary>
    public static ShellPropertyReader? TryOpen(string sourcePath)
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        try
        {
            var interfaceId = PropertyStoreId;
            var result = SHGetPropertyStoreFromParsingName(
                sourcePath,
                IntPtr.Zero,
                GetPropertyStoreFlags.Default,
                ref interfaceId,
                out var store);
            if (result < 0 || store is null)
            {
                return null;
            }

            return new ShellPropertyReader(store);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Lee un UI4 (32 bits sin signo). Falso si la propiedad no existe o tiene otro tipo.</summary>
    public bool TryGetUInt32(ShellPropertyKey key, out uint value)
    {
        value = 0;
        var property = new PropVariant();
        try
        {
            var propertyKey = key;
            if (_store is null
                || _store.GetValue(ref propertyKey, out property) < 0
                || property.VariantType != VtUi4)
            {
                return false;
            }

            value = (uint)property.UnsignedValue;
            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            PropVariantClear(ref property);
        }
    }

    /// <summary>Lee un UI8 (64 bits sin signo). Falso si la propiedad no existe o tiene otro tipo.</summary>
    public bool TryGetUInt64(ShellPropertyKey key, out ulong value)
    {
        value = 0;
        var property = new PropVariant();
        try
        {
            var propertyKey = key;
            if (_store is null
                || _store.GetValue(ref propertyKey, out property) < 0
                || property.VariantType != VtUi8)
            {
                return false;
            }

            value = property.UnsignedValue;
            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            PropVariantClear(ref property);
        }
    }

    /// <summary>Lee un texto (LPWSTR). Null si la propiedad no existe o no es texto.</summary>
    public string? TryGetString(ShellPropertyKey key)
    {
        var property = new PropVariant();
        try
        {
            var propertyKey = key;
            if (_store is null
                || _store.GetValue(ref propertyKey, out property) < 0
                || property.VariantType != VtLpwstr
                || property.Pointer == IntPtr.Zero)
            {
                return null;
            }

            return Marshal.PtrToStringUni(property.Pointer);
        }
        catch
        {
            return null;
        }
        finally
        {
            PropVariantClear(ref property);
        }
    }

    public void Dispose()
    {
        if (_store is not null)
        {
            Marshal.ReleaseComObject(_store);
            _store = null;
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
        int GetAt(uint index, out ShellPropertyKey key);

        [PreserveSig]
        int GetValue(ref ShellPropertyKey key, out PropVariant value);

        [PreserveSig]
        int SetValue(ref ShellPropertyKey key, ref PropVariant value);

        [PreserveSig]
        int Commit();
    }

    [StructLayout(LayoutKind.Explicit, Size = 24)]
    private struct PropVariant
    {
        [FieldOffset(0)]
        public ushort VariantType;

        [FieldOffset(8)]
        public ulong UnsignedValue;

        [FieldOffset(8)]
        public IntPtr Pointer;
    }

    [Flags]
    private enum GetPropertyStoreFlags
    {
        Default = 0
    }
}

/// <summary>
/// Clave de propiedad del Shell (PROPERTYKEY): fmtid + pid.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
internal struct ShellPropertyKey
{
    public ShellPropertyKey(Guid formatId, uint propertyId)
    {
        FormatId = formatId;
        PropertyId = propertyId;
    }

    public Guid FormatId;

    public uint PropertyId;
}
