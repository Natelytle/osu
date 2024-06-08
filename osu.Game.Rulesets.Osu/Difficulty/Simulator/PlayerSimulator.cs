// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Difficulty.Utils;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Difficulty.Simulator
{
    public class PlayerSimulator(List<OsuDifficultyHitObject> objects)
    {
        // Loops through simulateProbabiltities, returning the new attributes for every following instance of previousAttributes.
        public List<JudgementProbabilities> GetJudgementProbabilities(PlayerSkills playerSkills)
        {
            if (objects.Count == 0)
                return [];

            var secondObject = objects.First();

            // The first object in the list is actually the second object in the map, so we target it and set relevant attributes to the first object's data.
            PlayerSimulatorAttributes simulatorAttributes = new PlayerSimulatorAttributes
            {
                TargetObject = secondObject,
                PositionAtPrevNote = ((OsuHitObject)secondObject.LastObject).Position
            };

            // To keep the judgement probabilities length the same as the number of judgements, we add one guaranteed 300 for the first note.
            List<JudgementProbabilities> judgementProbabilitiesList = [new JudgementProbabilities()];

            while (simulatorAttributes.TargetObject is not null)
                judgementProbabilitiesList.AddRange(simulateProbabilities(playerSkills, simulatorAttributes, out simulatorAttributes));

            return judgementProbabilitiesList;
        }

        // Concepts to include:
        // Target notes - players may not play a map sequentially. For example, the 2nd notes in the doubles in this map are not specifically targeted.
        // Collateral objects - Raketapping and doubletapping don't work on a single note basis, so change the target note to whatever was not hit.
        // For example, the second notes in these doubles are implicitly hit once you hit the first note. https://youtu.be/y7UtN8km_TA?si=fFvbZQZpozASLIsD.
        // In addition, doubletapping only works when notes are stacked, but raketapping only works when the notes \aren't\ stacked.
        private JudgementProbabilities[] simulateProbabilities(PlayerSkills playerSkills, PlayerSimulatorAttributes previousAttributes, out PlayerSimulatorAttributes currentAttributes)
        {
            currentAttributes = new PlayerSimulatorAttributes();

            return [];
        }
    }
}
