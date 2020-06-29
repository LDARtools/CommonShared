using System;
using System.Collections.Generic;

namespace Ldartools.Common
{
    public class SoftwareVersion : IEquatable<SoftwareVersion>, IComparable<SoftwareVersion>, IComparable
    {
        public int Major { get; set; }
        public int Minor { get; set; }
        public int Tertiary { get; set; }
        public int Build { get; set; }

        #region Relational members

        public int CompareTo(SoftwareVersion other)
        {
            if (ReferenceEquals(this, other)) return 0;
            if (ReferenceEquals(null, other)) return 1;
            var majorComparison = Major.CompareTo(other.Major);
            if (majorComparison != 0) return majorComparison;
            var minorComparison = Minor.CompareTo(other.Minor);
            if (minorComparison != 0) return minorComparison;
            var tertiaryComparison = Tertiary.CompareTo(other.Tertiary);
            if (tertiaryComparison != 0) return tertiaryComparison;
            return Build.CompareTo(other.Build);
        }

        public int CompareTo(object obj)
        {
            if (ReferenceEquals(null, obj)) return 1;
            if (ReferenceEquals(this, obj)) return 0;
            if (!(obj is SoftwareVersion)) throw new ArgumentException($"Object must be of type {nameof(SoftwareVersion)}");
            return CompareTo((SoftwareVersion) obj);
        }

        public static bool operator <(SoftwareVersion left, SoftwareVersion right)
        {
            return Comparer<SoftwareVersion>.Default.Compare(left, right) < 0;
        }

        public static bool operator >(SoftwareVersion left, SoftwareVersion right)
        {
            return Comparer<SoftwareVersion>.Default.Compare(left, right) > 0;
        }

        public static bool operator <=(SoftwareVersion left, SoftwareVersion right)
        {
            return Comparer<SoftwareVersion>.Default.Compare(left, right) <= 0;
        }

        public static bool operator >=(SoftwareVersion left, SoftwareVersion right)
        {
            return Comparer<SoftwareVersion>.Default.Compare(left, right) >= 0;
        }

        #endregion

        #region Equality members

        public bool Equals(SoftwareVersion other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Major == other.Major && Minor == other.Minor && Tertiary == other.Tertiary && Build == other.Build;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((SoftwareVersion) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Major;
                hashCode = (hashCode * 397) ^ Minor;
                hashCode = (hashCode * 397) ^ Tertiary;
                hashCode = (hashCode * 397) ^ Build;
                return hashCode;
            }
        }

        public static bool operator ==(SoftwareVersion left, SoftwareVersion right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(SoftwareVersion left, SoftwareVersion right)
        {
            return !Equals(left, right);
        }

        #endregion

        public SoftwareVersion(int major, int minor, int tertiary, int build)
        {
            Major = major;
            Minor = minor;
            Tertiary = tertiary;
            Build = build;
        }

        public SoftwareVersion(Version version)
        {
            Major = version.Major;
            Minor = version.Minor;
            Tertiary = version.Build;
            Build = version.Revision;
        }

        public SoftwareVersion()
        {
        }

        public Version ToVersion()
        {
            return new Version(Major, Minor, Tertiary, Build);
        }

        #region Overrides of Object

        public override string ToString()
        {
            return $"{Major}.{Minor}.{Tertiary}.{Build}";
        }

        #endregion
    }
}
