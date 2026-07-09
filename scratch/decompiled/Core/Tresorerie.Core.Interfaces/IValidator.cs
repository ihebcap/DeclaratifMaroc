namespace Tresorerie.Core.Interfaces;

public interface IValidator<T>
{
	bool Validate(T type);
}
