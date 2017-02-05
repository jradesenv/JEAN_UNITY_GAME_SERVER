using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using static Enums;

public class Converters
{
    public static int ToValidMoveInput(float value)
    {
        int result;

        if (value == 0)
        {
            result = 0;
        }
        else if (value > 0)
        {
            result = 1;
        }
        else
        {
            result = -1;
        }

        return result;
    }

    public static string FloatToString(float value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }

    public static float StringToFloat(string text)
    {
        return float.Parse(text, System.Globalization.CultureInfo.InvariantCulture);
    }

    public static int StringToInt(string text)
    {
        return int.Parse(text);
    }

    public static string IntToString(int value)
    {
        return value.ToString();
    }

    public static DateTime StringToDateTime(string text)
    {
        return DateTime.ParseExact(text, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
    }

    public static string DateTimeToString(DateTime dateTime)
    {
        return dateTime.ToString("O");
    }

    public static string CharacterClassToString(CharacterClass characterClass)
    {
        return characterClass.ToString("d");
    }

    public static CharacterClass StringToCharacterClass(string characterClassValue)
    {
        return (CharacterClass)Enum.Parse(typeof(CharacterClass), characterClassValue);
    }
}
