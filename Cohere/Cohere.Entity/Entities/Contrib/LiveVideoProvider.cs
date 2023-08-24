using System;

namespace Cohere.Entity.Entities.Contrib
{
    public class LiveVideoProvider : IEquatable<LiveVideoProvider>
    {
        public string ProviderName { get; set; }

        public string CustomLink { get; set; }

        public override bool Equals(object obj)
        {
            return Equals(obj as LiveVideoProvider);
        }

        public bool Equals(LiveVideoProvider other)
        {
            return !ReferenceEquals(other, null) &&
                   ProviderName == other.ProviderName &&
                   CustomLink == other.CustomLink;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(ProviderName, CustomLink);
        }

        public static bool operator ==(LiveVideoProvider left, LiveVideoProvider right)
        {
            return !ReferenceEquals(left, null) && left.Equals(right);
        }

        public static bool operator !=(LiveVideoProvider left, LiveVideoProvider right)
        {
            return !(left == right);
        }
    }
}