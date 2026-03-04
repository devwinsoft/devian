// SSOT: skills/devian/10-module/20-core/14-version-number/SKILL.md

using System;

namespace Devian
{
    [Serializable]
    public struct VersionNumber : IComparable<VersionNumber>, IEquatable<VersionNumber>
    {
        public int Major;
        public int Minor;
        public int Patch;

        public VersionNumber(int major, int minor, int patch)
        {
            if (major < 0) throw new ArgumentOutOfRangeException(nameof(major), "Must be non-negative.");
            if (minor < 0) throw new ArgumentOutOfRangeException(nameof(minor), "Must be non-negative.");
            if (patch < 0) throw new ArgumentOutOfRangeException(nameof(patch), "Must be non-negative.");
            Major = major;
            Minor = minor;
            Patch = patch;
        }

        // ── Parse ──

        public static VersionNumber Parse(string value)
        {
            if (!TryParse(value, out var result))
                throw new FormatException($"Invalid version format: '{value}'. Expected '#.#.#'.");
            return result;
        }

        public static bool TryParse(string? value, out VersionNumber result)
        {
            result = default;
            if (string.IsNullOrEmpty(value)) return false;

            var parts = value.Split('.');
            if (parts.Length != 3) return false;

            if (!int.TryParse(parts[0], out int major) || major < 0) return false;
            if (!int.TryParse(parts[1], out int minor) || minor < 0) return false;
            if (!int.TryParse(parts[2], out int patch) || patch < 0) return false;

            result = new VersionNumber(major, minor, patch);
            return true;
        }

        // ── Comparison ──

        public int CompareTo(VersionNumber other)
        {
            int cmp = Major.CompareTo(other.Major);
            if (cmp != 0) return cmp;
            cmp = Minor.CompareTo(other.Minor);
            if (cmp != 0) return cmp;
            return Patch.CompareTo(other.Patch);
        }

        public static bool operator <(VersionNumber a, VersionNumber b) => a.CompareTo(b) < 0;
        public static bool operator >(VersionNumber a, VersionNumber b) => a.CompareTo(b) > 0;
        public static bool operator <=(VersionNumber a, VersionNumber b) => a.CompareTo(b) <= 0;
        public static bool operator >=(VersionNumber a, VersionNumber b) => a.CompareTo(b) >= 0;

        // ── Equality ──

        public bool Equals(VersionNumber other) =>
            Major == other.Major && Minor == other.Minor && Patch == other.Patch;

        public override bool Equals(object? obj) => obj is VersionNumber v && Equals(v);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Major;
                hash = hash * 397 ^ Minor;
                hash = hash * 397 ^ Patch;
                return hash;
            }
        }

        public static bool operator ==(VersionNumber left, VersionNumber right) => left.Equals(right);
        public static bool operator !=(VersionNumber left, VersionNumber right) => !left.Equals(right);

        // ── Display ──

        public override string ToString() => $"{Major}.{Minor}.{Patch}";
    }
}
