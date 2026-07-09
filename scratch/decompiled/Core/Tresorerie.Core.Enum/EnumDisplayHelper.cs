using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace Tresorerie.Core.Enum;

public static class EnumDisplayHelper
{
	public static string GetDisplayShortName(this System.Enum enumValue)
	{
		Type type = enumValue.GetType();
		if ((object)type == null)
		{
			return null;
		}
		MemberInfo[] member = type.GetMember(enumValue.ToString());
		if (member == null)
		{
			return null;
		}
		return member[0]?.GetCustomAttribute<DisplayAttribute>()?.ShortName;
	}

	public static string GetDisplayName(this System.Enum enumValue)
	{
		Type type = enumValue.GetType();
		if ((object)type == null)
		{
			return null;
		}
		MemberInfo[] member = type.GetMember(enumValue.ToString());
		if (member == null)
		{
			return null;
		}
		return member[0]?.GetCustomAttribute<DisplayAttribute>()?.Name;
	}

	public static string GetDisplayDescription(this System.Enum enumValue)
	{
		Type type = enumValue.GetType();
		object obj;
		if ((object)type == null)
		{
			obj = null;
		}
		else
		{
			MemberInfo[] member = type.GetMember(enumValue.ToString());
			obj = ((member == null) ? null : member[0]?.GetCustomAttribute<DisplayAttribute>()?.Description);
		}
		string text = (string)obj;
		if (!string.IsNullOrEmpty(text))
		{
			return text;
		}
		Type type2 = enumValue.GetType();
		object obj2;
		if ((object)type2 == null)
		{
			obj2 = null;
		}
		else
		{
			MemberInfo[] member2 = type2.GetMember(enumValue.ToString());
			obj2 = ((member2 == null) ? null : member2[0]?.GetCustomAttribute<DescriptionAttribute>()?.Description);
		}
		text = (string)obj2;
		if (!string.IsNullOrEmpty(text))
		{
			return text;
		}
		return enumValue.ToString();
	}
}
