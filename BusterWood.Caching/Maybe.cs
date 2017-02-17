using System;

namespace BusterWood.Caching
{
    public static class Maybe
    {
        public static Maybe<T> Some<T>(T value) => new Maybe<T>(value);

        public static Maybe<T> None<T>() => new Maybe<T>();
    }

    /// <summary>Optional value (Option monad).  Like <see cref="Nullable{T}"/> but for classes as well</summary>
    public struct Maybe<T> : IEquatable<Maybe<T>>
    {
        #pragma warning disable RECS0108 // Warns about static fields in generic types
        static readonly bool IsValueType = typeof(T).IsValueType; // avoid doing type checks in the constructor
        readonly T _value;

        public Maybe(T value)
        {
            _value = value;
            HasValue = IsValueType ? true : !ReferenceEquals(value, null);
        }

        /// <summary>TRUE if this represents a value, otherwise FALSE</summary>
        public bool HasValue { get; }

        /// <summary>The underlying value, if any.  Throws a <see cref="InvalidOperationException"/> is <see cref="HasValue"/> is FALSE.</summary>
        /// <exception cref="InvalidOperationException">Thrown if <see cref="HasValue"/> is FALSE.</exception>
        public T Value
        {
            get
            {
                if (!HasValue)
                    throw new InvalidOperationException("Maybe does not have a value");
                return _value;
            }
        }

        /// <summary>Indicates whether the current object is equal to another object of the same type.</summary>
        /// <returns>true if the current object is equal to the <paramref name="other" /> parameter; otherwise, false.</returns>
        /// <param name="other">An object to compare with this object.</param>
        public bool Equals(Maybe<T> other)
        {
            if (HasValue)
                return other.HasValue && _value.Equals(other._value);
            return !other.HasValue;
        }

        public override bool Equals(object obj)
        {
            if (obj is Maybe<T>)
                return Equals((Maybe<T>)obj);
            return HasValue ? Value.Equals(obj) : obj == null;
        }

        public override int GetHashCode() => HasValue ? _value.GetHashCode() : 0;

        /// <summary>
        /// Gets the underlying <see cref="Value"/> or Default(<typeparamref name="T"/>) if <see cref="HasValue"/> is FALSE.
        /// </summary>
        public T GetValueOrDefault() => _value;

        public override string ToString() => HasValue ? _value.ToString() : "(none)";

        public static bool operator ==(Maybe<T> left, object right) => left.Equals(right);
        public static bool operator ==(Maybe<T> left, Maybe<T> right) => left.Equals(right);

        public static bool operator !=(Maybe<T> left, object right) => !left.Equals(right);
        public static bool operator !=(Maybe<T> left, Maybe<T> right) => !left.Equals(right);

        public static implicit operator Maybe<T>(T value) => new Maybe<T>(value);

        public static explicit operator T(Maybe<T> value) => value.GetValueOrDefault(); //TODO: or Value?
    }
}
