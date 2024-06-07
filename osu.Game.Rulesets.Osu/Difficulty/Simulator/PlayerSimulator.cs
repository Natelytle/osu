// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Osu.Difficulty.Utils;

namespace osu.Game.Rulesets.Osu.Difficulty.Simulator
{
    public class PlayerSimulator(IBeatmap beatmap)
    {
        private readonly IBeatmap beatmap = beatmap;

        // Loops through simulateProbabiltities, returning the new attributes for every following instance of previousAttributes.
        public JudgementProbabilities[] GetJudgementProbabilities(PlayerSkills playerSkills)
        {
            throw new NotImplementedException();
        }

        // Concepts to include:
        // Target notes - players may not play a map sequentially. For example, the 2nd notes in the doubles in this map are not specifically targeted.
        // Collateral objects - Raketapping and doubletapping don't work on a single note basis, so change the target note to whatever was not hit.
        // For example, the second notes in these doubles are implicitly hit once you hit the first note. https://youtu.be/y7UtN8km_TA?si=fFvbZQZpozASLIsD.
        // In addition, doubletapping only works when notes are stacked, but raketapping only works when the notes \aren't\ stacked.
        private JudgementProbabilities[] simulateProbabilities(PlayerSkills playerSkills, PlayerSimulatorAttributes? previousAttributes, out PlayerSimulatorAttributes currentAttributes)
        {
            currentAttributes = new PlayerSimulatorAttributes();

            return [];
        }
    }
}
