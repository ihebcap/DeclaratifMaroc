using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using Tresorerie.Ressources;

namespace Tresorerie.Core.Enum;

public static class EnumHelper<T> where T : struct, IConvertible
{
	public static IEnumerable<EnumEx<T>> GetEnumExCollection()
	{
		if (!typeof(T).IsEnum)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorEnumType);
		}
		return System.Enum.GetValues(typeof(T)).Cast<T>().Select(delegate(T x)
		{
			string description = string.Empty;
			MemberInfo memberInfo = typeof(T).GetMember(x.ToString()).FirstOrDefault();
			if (memberInfo != null && memberInfo.GetCustomAttributes(typeof(DescriptionAttribute), inherit: false).FirstOrDefault() is DescriptionAttribute descriptionAttribute)
			{
				description = descriptionAttribute.Description;
			}
			return new EnumEx<T>
			{
				Value = x,
				Description = description
			};
		});
	}

	public static IEnumerable<EnumDisplayEx<T>> GetEnumDisplayExCollection()
	{
		if (!typeof(T).IsEnum)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorEnumType);
		}
		return System.Enum.GetValues(typeof(T)).Cast<T>().Select(delegate(T x)
		{
			System.Enum obj = x as System.Enum;
			string description = obj.GetDisplayDescription() ?? obj.ToString();
			string name = obj.GetDisplayName() ?? obj.ToString();
			return new EnumDisplayEx<T>
			{
				Value = x,
				Description = description,
				Name = name,
				ShortName = obj.GetDisplayShortName()
			};
		});
	}
}
