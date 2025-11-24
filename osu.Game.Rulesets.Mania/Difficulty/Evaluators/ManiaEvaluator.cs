// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Utils;
using osu.Game.Rulesets.Mania.Difficulty.Preprocessing;

namespace osu.Game.Rulesets.Mania.Difficulty.Evaluators
{
    internal abstract class ManiaEvaluator
    {
        private readonly List<ManiaChord> chords = new List<ManiaChord>();
        private readonly Dictionary<int, int> noteToChordIndex = new Dictionary<int, int>();

        protected ManiaEvaluator(ManiaDifficultyHitObject firstObject) =>
            buildChordMap(firstObject ?? throw new ArgumentNullException(nameof(firstObject)));

        public int ChordCount => chords.Count;

        protected IReadOnlyList<ManiaChord> Beatmap => chords;

        protected ManiaChord GetChordFor(ManiaDifficultyHitObject note)
        {
            if (!noteToChordIndex.TryGetValue(note.Index, out int chordIndex))
                throw new ArgumentException($"Note with index {note.Index} was not registered in the chord atlas.", nameof(note));

            return chords[chordIndex];
        }

        protected ManiaChord? GetPreviousChord(ManiaChord chord) =>
            chord.Index > 0 ? chords[chord.Index - 1] : null;

        protected ManiaChord? GetNextChord(ManiaChord chord) =>
            chord.Index < chords.Count - 1 ? chords[chord.Index + 1] : null;

        public abstract double EvaluateDifficultyOf(ManiaDifficultyHitObject obj);

        protected virtual double BpmToRatingCurve(double bpm) => 0;

        private void buildChordMap(ManiaDifficultyHitObject firstObject)
        {
            ManiaDifficultyHitObject? currentNote = firstObject;
            ManiaChord? currentChord = null;
            ManiaChord? previousChord = null;

            while (currentNote != null)
            {
                if (currentChord == null || !Precision.AlmostEquals(currentChord.StartTime, currentNote.StartTime))
                {
                    if (currentChord != null)
                    {
                        currentChord.Finalise(previousChord);
                        chords.Add(currentChord);
                        previousChord = currentChord;
                    }

                    currentChord = new ManiaChord(currentNote.StartTime)
                    {
                        Index = chords.Count
                    };
                }

                currentChord.AddNote(currentNote);
                noteToChordIndex[currentNote.Index] = currentChord.Index;

                currentNote = (ManiaDifficultyHitObject?)currentNote.Next(0);
            }

            if (currentChord != null)
            {
                currentChord.Finalise(previousChord);
                chords.Add(currentChord);
            }
        }
    }

    internal class ManiaChord
    {
        private readonly List<ManiaDifficultyHitObject> notes = new List<ManiaDifficultyHitObject>();

        public ManiaChord(double startTime)
        {
            StartTime = startTime;
        }

        public double StartTime { get; }
        public double DeltaTime { get; private set; } = double.PositiveInfinity;
        public double Bpm4 { get; private set; }
        public double Bpm2 { get; private set; }
        public int Index { get; set; }
        public IReadOnlyList<ManiaDifficultyHitObject> Notes => notes;

        public void AddNote(ManiaDifficultyHitObject note)
        {
            notes.Add(note);
        }

        public void Finalise(ManiaChord? previous)
        {
            DeltaTime = previous == null ? double.PositiveInfinity : StartTime - previous.StartTime;

            if (DeltaTime <= 0)
                DeltaTime = 1;

            if (double.IsPositiveInfinity(DeltaTime))
            {
                Bpm4 = 0;
                Bpm2 = 0;
                return;
            }

            Bpm4 = 15000 / DeltaTime;
            Bpm2 = 30000 / DeltaTime;
        }
    }
}
