using System;
using System.Collections.Generic;

namespace DeskPets
{
    /// <summary>A linked adjustment always starts from one snapshot, never from the previous mouse tick.</summary>
    public sealed class ScaleAdjustment
    {
        readonly Dictionary<string, int> sizes = new Dictionary<string, int>();
        readonly int backgroundHeight;
        public int AppliedPercent { get; private set; }
        public bool IsLimited { get; private set; }
        public int Count { get { return sizes.Count; } }

        ScaleAdjustment(UserSettings settings)
        {
            foreach (string id in settings.SelectedPets)
                if (!sizes.ContainsKey(id)) sizes.Add(id, settings.SizeFor(id));
            backgroundHeight = settings.BackgroundHeight;
            AppliedPercent = 100;
        }

        public static ScaleAdjustment Capture(UserSettings settings)
        {
            return new ScaleAdjustment(settings);
        }

        public int MinimumPercent(bool includeBackground)
        {
            double factor = 0.5;
            foreach (int size in sizes.Values) factor = Math.Max(factor, 50.0 / size);
            if (includeBackground) factor = Math.Max(factor, 32.0 / backgroundHeight);
            return Math.Min(100, (int)Math.Ceiling(factor * 100 - 0.000001));
        }

        public int MaximumPercent(bool includeBackground)
        {
            double factor = 2.0;
            foreach (int size in sizes.Values) factor = Math.Min(factor, 200.0 / size);
            if (includeBackground) factor = Math.Min(factor, 160.0 / backgroundHeight);
            return Math.Max(100, (int)Math.Floor(factor * 100 + 0.000001));
        }

        public void Apply(UserSettings settings, int percent, bool includeBackground)
        {
            int bounded = Math.Max(MinimumPercent(includeBackground), Math.Min(MaximumPercent(includeBackground), percent));
            IsLimited = bounded != percent;
            AppliedPercent = bounded;
            foreach (KeyValuePair<string, int> item in sizes)
                settings.PetSizes[item.Key] = Math.Max(50, Math.Min(200, (int)Math.Round(item.Value * bounded / 100.0)));
            if (includeBackground)
                settings.BackgroundHeight = Math.Max(32, Math.Min(160, (int)Math.Round(backgroundHeight * bounded / 100.0)));
        }
    }
}
