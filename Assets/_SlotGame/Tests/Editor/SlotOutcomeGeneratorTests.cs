using System;
using NUnit.Framework;
using SlotGame.Configuration;
using SlotGame.Gameplay;
using UnityEditor;
using UnityEngine;
using Random = System.Random;

namespace SlotGame.Tests.Gameplay
{
    public sealed class SlotOutcomeGeneratorTests
    {
        private SlotGameConfig config;
        private Texture2D texture;
        private Sprite sprite;

        #region Test Setup

        [SetUp]
        public void SetUp()
        {
            config = ScriptableObject.CreateInstance<SlotGameConfig>();
            texture = new Texture2D(1, 1);
            sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f));

            SlotSymbolId[] ids =
            {
                SlotSymbolId.Seven,
                SlotSymbolId.Cherries,
                SlotSymbolId.Bell,
                SlotSymbolId.Bar
            };

            int[] multipliers = { 30, 5, 10, 15 };
            int[] bets = { 10, 25, 50 };

            // Configure an in-memory fixture without exposing runtime setters.
            using (SerializedObject serialized = new SerializedObject(config))
            {
                serialized.FindProperty("startingCredits").intValue = 1000;

                SerializedProperty betPresets =
                    serialized.FindProperty("betPresets");

                betPresets.arraySize = bets.Length;

                for (int i = 0; i < bets.Length; i++)
                {
                    betPresets.GetArrayElementAtIndex(i).intValue = bets[i];
                }

                SerializedProperty symbols = serialized.FindProperty("symbols");
                symbols.arraySize = ids.Length;

                for (int i = 0; i < ids.Length; i++)
                {
                    SerializedProperty symbol =
                        symbols.GetArrayElementAtIndex(i);

                    symbol.FindPropertyRelative("id").intValue = (int)ids[i];
                    symbol.FindPropertyRelative("displayName").stringValue =
                        ids[i].ToString();
                    symbol.FindPropertyRelative("sprite").objectReferenceValue =
                        sprite;
                    symbol.FindPropertyRelative("weight").floatValue = 1f;
                    symbol.FindPropertyRelative("payoutMultiplier").intValue =
                        multipliers[i];
                }

                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        [TearDown]
        public void TearDown()
        {
            if (config != null)
            {
                UnityEngine.Object.DestroyImmediate(config);
            }

            if (sprite != null)
            {
                UnityEngine.Object.DestroyImmediate(sprite);
            }

            if (texture != null)
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        #endregion

        #region Generator Tests

        [TestCase(1f, 1f, 1f, 1f, 0d, SlotSymbolId.Seven, 300)]
        [TestCase(0f, 1f, 0f, 1f, 0d, SlotSymbolId.Cherries, 50)]
        public void Generate_UsesConfiguredWeightsAndMatchingPayout(
            float sevenWeight,
            float cherriesWeight,
            float bellWeight,
            float barWeight,
            double sample,
            SlotSymbolId expectedSymbol,
            int expectedPayout)
        {
            SetWeights(
                sevenWeight,
                cherriesWeight,
                bellWeight,
                barWeight);

            SequenceRandom random = new SequenceRandom(sample, sample, sample);
            SlotOutcomeGenerator generator =
                new SlotOutcomeGenerator(config, random);

            SpinResult result = generator.Generate(10);

            Assert.That(result.LeftSymbol, Is.EqualTo(expectedSymbol));
            Assert.That(result.MiddleSymbol, Is.EqualTo(expectedSymbol));
            Assert.That(result.RightSymbol, Is.EqualTo(expectedSymbol));
            Assert.That(result.Bet, Is.EqualTo(10));
            Assert.That(result.Payout, Is.EqualTo(expectedPayout));
            Assert.That(result.IsWin, Is.True);
            Assert.That(random.Calls, Is.EqualTo(3));
        }

        [Test]
        public void Generate_MixedSymbolsReturnZeroPayout()
        {
            SequenceRandom random = new SequenceRandom(0d, 0.25d, 0.5d);
            SlotOutcomeGenerator generator =
                new SlotOutcomeGenerator(config, random);

            SpinResult result = generator.Generate(25);

            Assert.That(result.LeftSymbol, Is.EqualTo(SlotSymbolId.Seven));
            Assert.That(result.MiddleSymbol, Is.EqualTo(SlotSymbolId.Cherries));
            Assert.That(result.RightSymbol, Is.EqualTo(SlotSymbolId.Bell));
            Assert.That(result.Bet, Is.EqualTo(25));
            Assert.That(result.Payout, Is.Zero);
            Assert.That(result.IsWin, Is.False);
            Assert.That(random.Calls, Is.EqualTo(3));
        }

        [TestCase(15)]
        public void Generate_InvalidBetDoesNotConsumeRandomValues(int bet)
        {
            SequenceRandom random = new SequenceRandom(0d, 0.25d, 0.5d);
            SlotOutcomeGenerator generator =
                new SlotOutcomeGenerator(config, random);

            Assert.Throws<ArgumentOutOfRangeException>(
                () => generator.Generate(bet));

            Assert.That(random.Calls, Is.Zero);

            SpinResult result = generator.Generate(10);

            Assert.That(result.LeftSymbol, Is.EqualTo(SlotSymbolId.Seven));
            Assert.That(result.MiddleSymbol, Is.EqualTo(SlotSymbolId.Cherries));
            Assert.That(result.RightSymbol, Is.EqualTo(SlotSymbolId.Bell));
            Assert.That(result.Bet, Is.EqualTo(10));
            Assert.That(result.Payout, Is.Zero);
            Assert.That(random.Calls, Is.EqualTo(3));
        }

        #endregion

        #region Test Helpers

        private void SetWeights(params float[] weights)
        {
            using (SerializedObject serialized = new SerializedObject(config))
            {
                SerializedProperty symbols = serialized.FindProperty("symbols");

                for (int i = 0; i < weights.Length; i++)
                {
                    symbols.GetArrayElementAtIndex(i)
                        .FindPropertyRelative("weight").floatValue = weights[i];
                }

                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        /// <summary>
        /// Supplies known valid RNG samples so selection checks are repeatable.
        /// </summary>
        private sealed class SequenceRandom : Random
        {
            private readonly double[] samples;
            private int nextIndex;

            public int Calls => nextIndex;

            public SequenceRandom(params double[] samples)
            {
                this.samples = samples;
            }

            public override double NextDouble()
            {
                if (nextIndex >= samples.Length)
                {
                    throw new InvalidOperationException(
                        "The generator requested more RNG samples than expected.");
                }

                return samples[nextIndex++];
            }
        }

        #endregion
    }
}