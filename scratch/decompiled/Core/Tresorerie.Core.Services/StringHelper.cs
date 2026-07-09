using System;
using System.Text.RegularExpressions;

namespace Tresorerie.Core.Services;

public static class StringHelper
{
	public static string Increment(this string piece)
	{
		Match match = new Regex("([A-Z]*)([0-9]*)").Match(piece);
		string value = match.Groups[1].Value;
		string value2 = match.Groups[2].Value;
		string text = new string('0', value2.Length);
		string arg = (long.Parse(value2) + 1).ToString(text);
		return $"{value}{arg}";
	}

	public static string IncrementCode(string intCode)
	{
		char[] array = intCode.ToCharArray();
		int num = array.Length - 1;
		bool flag = true;
		while (num >= 0 && flag)
		{
			if (char.IsDigit(array[num]))
			{
				if (array[num] == '9')
				{
					array[num] = '0';
					num--;
				}
				else
				{
					array[num] = Convert.ToChar(Convert.ToInt32(array[num]) + 1);
					flag = false;
				}
			}
			else if (array[num] == 'Z')
			{
				array[num] = 'A';
				num--;
			}
			else
			{
				if (array[num] >= 'A' && array[num] <= 'Z')
				{
					array[num] = Convert.ToChar(Convert.ToInt32(array[num]) + 1);
				}
				flag = false;
			}
		}
		intCode = string.Empty;
		for (int i = 0; i < array.Length; i++)
		{
			intCode += array[i];
		}
		if (int.TryParse(intCode, out var result) && result == 0)
		{
			intCode = "1" + intCode;
		}
		return intCode;
	}

	public static bool IsValid(this string value)
	{
		if (!string.IsNullOrEmpty(value))
		{
			return !string.IsNullOrWhiteSpace(value);
		}
		return false;
	}
}
