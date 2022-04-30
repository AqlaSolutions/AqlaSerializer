using System;

namespace AqlaSerializer.Meta
{
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
#if !PORTABLE
    [System.Serializable]
#endif
    public class CompiledAssemblyEqualityAttribute : Attribute
    {
        public string Hash { get; private set; }
        public int Length { get; private set; }
        public int AqlaVersionMajor { get; private set; }
        public int AqlaVersionMinor { get; private set; }
        public int AqlaVersionRevision { get; private set; }
        public int AqlaVersionBuild { get; private set; }
        public bool IsPublic { get; private set; }

        public object[] GetConstructorArgs()
        {
            return new object[] { AqlaVersionMajor, AqlaVersionMinor, AqlaVersionRevision, AqlaVersionBuild, Hash, Length, IsPublic };
        }

        public CompiledAssemblyEqualityAttribute(int aqlaVersionMajor, int aqlaVersionMinor, int aqlaVersionRevision, int aqlaVersionBuild, string hash, int length, bool isPublic)
        {
            AqlaVersionBuild = aqlaVersionBuild;
            AqlaVersionMajor = aqlaVersionMajor;
            AqlaVersionMinor = aqlaVersionMinor;
            AqlaVersionRevision = aqlaVersionRevision;
            Hash = hash;
            Length = length;
            IsPublic = isPublic;
        }

        protected bool Equals(CompiledAssemblyEqualityAttribute other)
        {
            return base.Equals(other) && string.Equals(Hash, other.Hash) && Length == other.Length && AqlaVersionMajor == other.AqlaVersionMajor &&
                   AqlaVersionMinor == other.AqlaVersionMinor && AqlaVersionRevision == other.AqlaVersionRevision && AqlaVersionBuild == other.AqlaVersionBuild
                   && IsPublic == other.IsPublic;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((CompiledAssemblyEqualityAttribute)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = base.GetHashCode();
                hashCode = (hashCode * 397) ^ (Hash != null ? Hash.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ Length;
                hashCode = (hashCode * 397) ^ AqlaVersionMajor;
                hashCode = (hashCode * 397) ^ AqlaVersionMinor;
                hashCode = (hashCode * 397) ^ AqlaVersionRevision;
                hashCode = (hashCode * 397) ^ AqlaVersionBuild;
                return hashCode;
            }
        }

        public static bool operator ==(CompiledAssemblyEqualityAttribute left, CompiledAssemblyEqualityAttribute right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(CompiledAssemblyEqualityAttribute left, CompiledAssemblyEqualityAttribute right)
        {
            return !Equals(left, right);
        }
    }
}