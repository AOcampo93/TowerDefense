using System.Collections.Generic;
using UnityEngine;

namespace AlienInvaders.Views
{
    /// <summary>
    /// A particle effect from a store pack (an optional prefab) attached to one of our views when the game loads.
    /// Store effects are made for demo scenes: they start by themselves, often loop and may ignore the scale of their
    /// parent. Here every system waits to be told to play, plays once (or keeps looping when asked) and scales with
    /// its parent, so one effect serves explosions of any size. Without the pack the prefab is missing, Attach returns
    /// null and the view keeps its own greybox look.
    /// </summary>
    public class ParticleEffect
    {
        // Systems with no particle system above them: playing or clearing them does the same to their children.
        private readonly List<ParticleSystem> _roots = new List<ParticleSystem>();

        /// <summary>Seconds from the start until the last particle of a one shot effect is gone.</summary>
        public float Duration { get; private set; }

        /// <summary>
        /// Copies the effect under the parent at this scale, its colors multiplied by the tint; null when the store
        /// asset is missing.
        /// </summary>
        public static ParticleEffect Attach(GameObject prefab, Transform parent, float scale, bool loop, Color tint)
        {
            if (prefab == null)
            {
                return null;
            }
            GameObject instance = Object.Instantiate(prefab, parent, false);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one * scale;

            var effect = new ParticleEffect();
            foreach (ParticleSystem system in instance.GetComponentsInChildren<ParticleSystem>(true))
            {
                system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                ParticleSystem.MainModule main = system.main;
                main.loop = loop;
                main.playOnAwake = false;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                main.stopAction = ParticleSystemStopAction.None;
                main.startColor = Tinted(main.startColor, tint);
                // Particles hitting the world and real lights cost too much on the headset.
                ParticleSystem.CollisionModule collision = system.collision;
                collision.enabled = false;
                ParticleSystem.LightsModule lights = system.lights;
                lights.enabled = false;
                effect.Duration = Mathf.Max(effect.Duration,
                    main.startDelay.constantMax + main.duration + main.startLifetime.constantMax);
                Transform above = system.transform.parent;
                if (above == null || above.GetComponentInParent<ParticleSystem>() == null)
                {
                    effect._roots.Add(system);
                }
            }
            return effect;
        }

        /// <summary>Same as Attach with the effect's own colors.</summary>
        public static ParticleEffect Attach(GameObject prefab, Transform parent, float scale, bool loop)
        {
            return Attach(prefab, parent, scale, loop, Color.white);
        }

        /// <summary>Plays the effect from its start, cutting short whatever was still showing.</summary>
        public void Restart()
        {
            foreach (ParticleSystem system in _roots)
            {
                system.Clear(true);
                system.Play(true);
            }
        }

        /// <summary>Stops the effect and clears its particles.</summary>
        public void Stop()
        {
            foreach (ParticleSystem system in _roots)
            {
                system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        // The start color of a system multiplied by the tint, whatever form it takes (one color, two, or gradients).
        private static ParticleSystem.MinMaxGradient Tinted(ParticleSystem.MinMaxGradient color, Color tint)
        {
            if (tint == Color.white)
            {
                return color;
            }
            switch (color.mode)
            {
                case ParticleSystemGradientMode.Color:
                    return new ParticleSystem.MinMaxGradient(color.color * tint);
                case ParticleSystemGradientMode.TwoColors:
                    return new ParticleSystem.MinMaxGradient(color.colorMin * tint, color.colorMax * tint);
                case ParticleSystemGradientMode.TwoGradients:
                    return new ParticleSystem.MinMaxGradient(Tinted(color.gradientMin, tint), Tinted(color.gradientMax, tint));
                default:
                    var tinted = new ParticleSystem.MinMaxGradient(Tinted(color.gradient, tint));
                    tinted.mode = color.mode;
                    return tinted;
            }
        }

        private static Gradient Tinted(Gradient gradient, Color tint)
        {
            GradientColorKey[] keys = gradient.colorKeys;
            for (int i = 0; i < keys.Length; i++)
            {
                keys[i].color *= tint;
            }
            var tinted = new Gradient { mode = gradient.mode };
            tinted.SetKeys(keys, gradient.alphaKeys);
            return tinted;
        }
    }
}
