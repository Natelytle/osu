// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Osu.Objects.Drawables;
using osu.Game.Rulesets.Osu.Scoring;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI;

namespace osu.Game.Rulesets.Osu.UI
{
    /// <summary>
    /// Ensures that <see cref="HitObject"/>s are hit in order of appearance. The classic note lock.
    /// <remarks>
    /// Hits will be blocked until the previous <see cref="HitObject"/>s have been judged.
    /// </remarks>
    /// </summary>
    public class LegacyHitPolicy : IHitPolicy
    {
        public IHitObjectContainer? HitObjectContainer { get; set; }

        private readonly double hittableRange;

        public LegacyHitPolicy(double hittableRange = OsuHitWindows.MISS_WINDOW)
        {
            this.hittableRange = hittableRange;
        }

        public virtual ClickAction CheckHittable(DrawableHitObject hitObject, double time, HitResult result)
        {
            if (HitObjectContainer == null)
                throw new InvalidOperationException($"{nameof(HitObjectContainer)} should be set before {nameof(CheckHittable)} is called.");

            List<DrawableHitObject> aliveObjectsWithNested = new List<DrawableHitObject>();

            foreach (var o in HitObjectContainer.AliveObjects)
            {
                if (o is not DrawableSlider)
                {
                    aliveObjectsWithNested.Add(o);
                }

                aliveObjectsWithNested.AddRange(o.NestedHitObjects);
            }

            int index = aliveObjectsWithNested.IndexOf(hitObject);

            if (index > 0)
            {
                var previousHitObjects = aliveObjectsWithNested.GetRange(0, index).ConvertAll(o => (DrawableOsuHitObject)o);

                var previousHitObject = previousHitObjects.Last();

                // If the click happens after the slider end would be finished
                if (previousHitObjects.Where(o => !o.AllJudged).All(hitObjectIsSliderObject) && previousHitObject.HitObject.GetEndTime() < time)
                {
                    return ClickAction.Hit;
                }

                if (previousHitObject.HitObject.StackHeight > 0 && !previousHitObject.AllJudged)
                    return ClickAction.Ignore;
            }

            if (result == HitResult.None)
                return ClickAction.Shake;

            foreach (DrawableHitObject testObject in aliveObjectsWithNested)
            {
                if (testObject.AllJudged)
                    continue;

                // if we found the object being checked, we can move on to the final timing test.
                if (testObject == hitObject)
                    break;

                // for all other objects, we check for validity and block the hit if any are still valid.
                // 3ms of extra leniency to account for slightly unsnapped objects.
                if (testObject.HitObject.GetEndTime() + 3 < hitObject.HitObject.StartTime)
                    return ClickAction.Shake;
            }

            return Math.Abs(hitObject.HitObject.StartTime - time) < hittableRange ? ClickAction.Hit : ClickAction.Shake;
        }

        public void HandleHit(DrawableHitObject hitObject)
        {
            if (HitObjectContainer == null)
                throw new InvalidOperationException($"{nameof(HitObjectContainer)} should be set before {nameof(HandleHit)} is called.");

            // Hitobjects which themselves don't block future hitobjects don't cause misses (e.g. slider ticks, spinners).
            if (!hitObjectCanBlockFutureHits(hitObject))
                return;

            if (CheckHittable(hitObject, hitObject.HitObject.StartTime + hitObject.Result.TimeOffset, hitObject.Result.Type) != ClickAction.Hit)
                throw new InvalidOperationException($"A {hitObject} was hit before it became hittable!");

            // Miss all hitobjects prior to the hit one.
            foreach (var obj in enumerateHitObjectsUpTo(hitObject.HitObject.StartTime))
            {
                if (obj.Judged)
                    continue;

                if (hitObjectCanBlockFutureHits(obj))
                    ((DrawableOsuHitObject)obj).MissForcefully();
            }
        }

        /// <summary>
        /// Whether a <see cref="HitObject"/> blocks hits on future <see cref="HitObject"/>s until its start time is reached.
        /// </summary>
        /// <param name="hitObject">The <see cref="HitObject"/> to test.</param>
        private static bool hitObjectCanBlockFutureHits(DrawableHitObject hitObject)
            => hitObject is DrawableHitCircle;

        private static bool hitObjectIsSliderObject(DrawableHitObject hitObject)
            => hitObject is DrawableSlider or DrawableSliderHead or DrawableSliderTick or DrawableSliderTail;

        private IEnumerable<DrawableHitObject> enumerateHitObjectsUpTo(double targetTime)
        {
            foreach (var obj in HitObjectContainer!.AliveObjects)
            {
                if (obj.HitObject.StartTime >= targetTime)
                    yield break;

                yield return obj;

                foreach (var nestedObj in obj.NestedHitObjects)
                {
                    if (nestedObj.HitObject.StartTime >= targetTime)
                        break;

                    yield return nestedObj;
                }
            }
        }
    }
}
