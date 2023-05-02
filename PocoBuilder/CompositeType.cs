namespace PocoBuilder
{
    public class UnresolvableCompositeTypeException : Exception { }
    public readonly struct CompositeType<T1, T2>
    {
        readonly int type; readonly object? entity;
        public CompositeType(T1? value) { entity = value; type = 1; }
        public CompositeType(T2? value) { entity = value; type = 2; }
        public void Resolve(Action<T1?> a1, Action<T2?> a2)
        {
            switch (type)
            {
                case 1: a1((T1?)entity); break;
                case 2: a2((T2?)entity); break;
                default: throw new UnresolvableCompositeTypeException();
            }
        }
        public static implicit operator T1?(CompositeType<T1, T2> value) => (T1?)value.entity;
        public static implicit operator T2?(CompositeType<T1, T2> value) => (T2?)value.entity;
        public static implicit operator CompositeType<T1, T2>(T1? value) => new(value);
        public static implicit operator CompositeType<T1, T2>(T2? value) => new(value);
    }
    public readonly struct CompositeType<T1, T2, T3>
    {
        readonly int type; readonly object? entity;
        public CompositeType(T1? value) { entity = value; type = 1; }
        public CompositeType(T2? value) { entity = value; type = 2; }
        public CompositeType(T3? value) { entity = value; type = 3; }
        public void Resolve(Action<T1?> a1, Action<T2?> a2, Action<T3?> a3)
        {
            switch (type)
            {
                case 1: a1((T1?)entity); break;
                case 2: a2((T2?)entity); break;
                case 3: a3((T3?)entity); break;
                default: throw new UnresolvableCompositeTypeException();
            }
        }
        public static implicit operator T1?(CompositeType<T1, T2, T3> value) => (T1?)value.entity;
        public static implicit operator T2?(CompositeType<T1, T2, T3> value) => (T2?)value.entity;
        public static implicit operator T3?(CompositeType<T1, T2, T3> value) => (T3?)value.entity;
        public static implicit operator CompositeType<T1, T2, T3>(T1? value) => new(value);
        public static implicit operator CompositeType<T1, T2, T3>(T2? value) => new(value);
        public static implicit operator CompositeType<T1, T2, T3>(T3? value) => new(value);
    }
}
