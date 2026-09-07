public abstract class CustomDataValue{}

public sealed class CustomDataValue<T> : CustomDataValue
{
    public readonly T Value;
    public CustomDataValue(T value) => Value = value;
}

public static class CustomDataValueFactory
{
    public static CustomDataValue Of<T>(T v) => new CustomDataValue<T>(v);
}