using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CMGAS
{
    public readonly struct CMName : IEquatable<CMName>, IComparable<CMName>
    {
        internal static readonly CMName None = default;
        private readonly int value;

        internal CMName(int value) => this.value = value;

        public bool IsValid => value != 0;
        public bool Equals(CMName other) => value == other.value;
        public override bool Equals(object obj) => obj is CMName other && Equals(other);
        public override int GetHashCode() => value;
        public int CompareTo(CMName other) => value.CompareTo(other.value);
        public static bool operator ==(CMName a, CMName b) => a.value == b.value;
        public static bool operator !=(CMName a, CMName b) => a.value != b.value;
    }
}


