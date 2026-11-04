namespace Revit26_Plugin.BubbleAutoRenumber.V009.Core.Models
{
    /// <summary>How each detail number is written. Numbers is the default (the old behaviour).</summary>
    public enum NumberFormat
    {
        /// <summary>1, 2, 3…</summary>
        Numbers = 0,

        /// <summary>01, 02, 03… (two digits minimum; 100 and up print as is).</summary>
        ZeroPadded = 1,

        /// <summary>A, B, C… Z, AA… — I and O skipped.</summary>
        UpperLetters = 2,

        /// <summary>a, b, c… z, aa… — i and o skipped.</summary>
        LowerLetters = 3,

        /// <summary>A1, A2, A3… — a fixed prefix followed by a number.</summary>
        PrefixNumber = 4
    }

    /// <summary>One entry in the window's Number Format dropdown.</summary>
    public class NumberFormatOption
    {
        /// <summary>The format this entry selects.</summary>
        public NumberFormat Format { get; }

        /// <summary>Text shown in the dropdown.</summary>
        public string Label { get; }

        /// <summary>Creates a dropdown entry.</summary>
        public NumberFormatOption(NumberFormat format, string label)
        {
            Format = format;
            Label = label;
        }

        /// <summary>All entries, in dropdown order.</summary>
        public static NumberFormatOption[] All { get; } =
        {
            new(NumberFormat.Numbers, "1, 2, 3"),
            new(NumberFormat.ZeroPadded, "01, 02, 03"),
            new(NumberFormat.UpperLetters, "A, B, C  (no I, O)"),
            new(NumberFormat.LowerLetters, "a, b, c  (no i, o)"),
            new(NumberFormat.PrefixNumber, "A1, A2, A3  (prefix + number)")
        };
    }
}
