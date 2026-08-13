using System.ComponentModel;

namespace TheMurderStoneArchive.Helpers;

/// <summary>
/// A minimal TypeConverter registered on NTS <see cref="NetTopologySuite.Geometries.Geometry"/>
/// so that ASP.NET Core's DefaultModelMetadataProvider treats the entire geometry type hierarchy
/// as a simple scalar rather than a complex object.
///
/// Without this, the metadata system recursively enumerates Geometry properties
/// (Boundary → Geometry → Boundary → ...) until the call stack is exhausted, which throws
/// an InsufficientExecutionStackException on any form page that binds a model containing
/// a geometry-typed property (such as Location.Coordinates).
///
/// Returning true from CanConvertFrom(string) is all that is required — it signals "simple
/// type" to the metadata system and prevents child-property enumeration entirely.
/// </summary>
internal sealed class NtsGeometryTypeConverter : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
        => sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

    // Return null — the form binder never actually calls this because Location.Coordinates
    // is also marked [BindNever], so the converter exists purely to satisfy the metadata check.
    public override object? ConvertFrom(ITypeDescriptorContext? context,
        System.Globalization.CultureInfo? culture, object value) => null;
}
