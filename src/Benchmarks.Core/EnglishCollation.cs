using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Benchmarks.Core;

/// <summary>
/// Registers the named <c>collation_en_us</c> collation used by the Collection.db note tables.
/// Mirrors the English collation used by the source application: case-, diacritic-, width-
/// and kana-insensitive comparison based on the en-US culture.
/// </summary>
public static class EnglishCollation
{
    public const string Name = "collation_en_us";

    private const CompareOptions Options =
        CompareOptions.IgnoreCase |
        CompareOptions.IgnoreNonSpace |
        CompareOptions.IgnoreWidth |
        CompareOptions.IgnoreKanaType;

    private static readonly CompareInfo Compare = GetEnglishCompareInfo();

    private static CompareInfo GetEnglishCompareInfo()
    {
        try
        {
            return CultureInfo.GetCultureInfo("en-US").CompareInfo;
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.InvariantCulture.CompareInfo;
        }
    }

    /// <summary>Registers the collation on an open connection.</summary>
    public static void Register(SqliteConnection connection, string name = Name)
    {
        ArgumentNullException.ThrowIfNull(connection);
        connection.CreateCollation(name, (x, y) =>
        {
            if (x is null && y is null) return 0;
            if (x is null) return -1;
            if (y is null) return 1;
            return Compare.Compare(x, y, Options);
        });
    }
}
