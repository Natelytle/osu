// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Taiko.Objects;
using osu.Game.Rulesets.Taiko.Objects.Drawables;

namespace osu.Game.Rulesets.Taiko.Mods
{
    public class TaikoModGhosting : Mod, IApplicableToDrawableHitObject
    {
        public override string Name => "Ghosting";
        public override string Acronym => "GH";
        public override LocalisableString Description => @"Hit opposite colours in empty spaces!";
        public override double ScoreMultiplier => 0.95;
        public override IconUsage? Icon => OsuIcon.ModNoFail;
        public override ModType Type => ModType.DifficultyReduction;

        public override Type[] IncompatibleMods => new[] { typeof(TaikoModRelax) };

        public void ApplyToDrawableHitObject(DrawableHitObject drawable)
        {
            drawable.HitObjectApplied += dho =>
            {
                switch (dho)
                {
                    case DrawableHit hit:
                        var otherColourActions = hit.HitObject.Type == HitType.Centre
                            ? new[] { TaikoAction.LeftRim, TaikoAction.RightRim }
                            : new[] { TaikoAction.LeftCentre, TaikoAction.RightCentre };

                        hit.IgnoreActions = otherColourActions;
                        break;
                }
            };
        }
    }
}
