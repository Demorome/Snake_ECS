
using System;
using System.Drawing;

namespace ContentBuilderUI;

public static class ColorCodeGen
{
    public static string GenerateDeclarations()
    {
        string result = string.Empty;
        foreach (var colorType in Enum.GetValues<KnownColor>())
        {
            if ((colorType >= KnownColor.AliceBlue && colorType <= KnownColor.YellowGreen)
                || colorType == KnownColor.RebeccaPurple)
            {
                var color = Color.FromKnownColor(colorType);
                //var moonWorksColor = new MoonWorks.Graphics.Color(color.R, color.G, color.B, color.A);
                //var packedColor = moonWorksColor.PackedValue();
                // Above result isn't too useful when printed out due to reversed endianness.
                var colorAsHex = string.Format("{0:X2}{1:X2}{2:X2}{3:X2}", 
                    color.R, color.G, color.B, color.A);

                // Comment
                result += "/// <summary>\n";
                result += $"/// {colorType} color (R:{color.R}, G:{color.G}, B:{color.B}, A:{color.A}).\n";
                result += $"/// <para>RGBA Hex: #{colorAsHex}.</para>\n";
                result += "/// </summary>\n";

                // Declaration
                result += $"public static Color {colorType} => new((byte){color.R}, (byte){color.G}, (byte){color.B});\n";
                result += "\n";
            }
        }
        return result;
    }
}