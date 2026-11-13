using System;
using NUnit.Framework;
using SlotGame.Configuration;
using SlotGame.Gameplay;
using UnityEditor;
using UnityEngine;
using Random = System.Random;

namespace SlotGame.Tests.Gameplay
{
    public sealed class SlotSessionTests
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

            using (SerializedObject serialized = new SerializedObject(config))
            {
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

            SetSessionSettings(1000, 10, 25, 50);
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

        #region Session Tests

        [TestCase(0d, 0d, 0d, 300)]
        [TestCase(0d, 0.25d, 0.5d, 0)]
        public void Spin_DeductsBetAndSettlesOnlyOnce(
            double leftSample,
            double middleSample,
            double rightSample,
            int expectedPayout)
        {
            SequenceRandom random =
                new SequenceRandom(leftSample, middleSample, rightSample);

            SlotSession session = new SlotSession(config, random);

            Assert.That(session.TryCompleteSpin(), Is.False);

            bool started = session.TryBeginSpin(
                out SpinResult result,
                out string error);

            Assert.That(started, Is.True, error);
            Assert.That(session.Credits, Is.EqualTo(990));
            Assert.That(result.Bet, Is.EqualTo(10));
            Assert.That(result.Payout, Is.EqualTo(expectedPayout));
            Assert.That(session.PendingResult, Is.SameAs(result));
            Assert.That(session.LatestResult, Is.Null);
            Assert.That(session.IsSpinning, Is.True);
            Assert.That(session.CanSpin, Is.False);
            Assert.That(random.Calls, Is.EqualTo(3));

            Assert.That(session.TryCompleteSpin(), Is.True);
            Assert.That(session.Credits, Is.EqualTo(990 + expectedPayout));
            Assert.That(session.LatestResult, Is.SameAs(result));
            Assert.That(session.PendingResult, Is.Null);
            Assert.That(session.IsSpinning, Is.False);

            Assert.That(session.TryCompleteSpin(), Is.False);
            Assert.That(session.Credits, Is.EqualTo(990 + expectedPayout));
            Assert.That(random.Calls, Is.EqualTo(3));
        }

        [Test]
        public void Spin_RejectsDuplicateStartBetChangesAndReset()
        {
            SequenceRandom random = new SequenceRandom(0d, 0d, 0d);
            SlotSession session = new SlotSession(config, random);

            Assert.That(
                session.TryBeginSpin(out SpinResult firstResult, out _),
                Is.True);

            bool duplicateAccepted = session.TryBeginSpin(
                out SpinResult duplicateResult,
                out string error);

            Assert.That(duplicateAccepted, Is.False);
            Assert.That(duplicateResult, Is.Null);
            Assert.That(error, Is.Not.Empty);
            Assert.That(session.TrySelectBet(1), Is.False);
            Assert.That(session.TryReset(), Is.False);

            Assert.That(session.SelectedBet, Is.EqualTo(10));
            Assert.That(session.Credits, Is.EqualTo(990));
            Assert.That(session.PendingResult, Is.SameAs(firstResult));
            Assert.That(session.IsSpinning, Is.True);
            Assert.That(random.Calls, Is.EqualTo(3));

            Assert.That(session.TryCompleteSpin(), Is.True);
            Assert.That(session.Credits, Is.EqualTo(1290));
        }

        [Test]
        public void Spin_UnaffordableBetAllowsSmallerBetRecovery()
        {
            SetSessionSettings(20, 10, 25, 50);

            SequenceRandom random =
                new SequenceRandom(0d, 0.25d, 0.5d, 0d, 0.25d, 0.5d);

            SlotSession session = new SlotSession(config, random);

            Assert.That(session.TrySelectBet(1), Is.True);
            Assert.That(session.CanAffordSelectedBet, Is.False);
            Assert.That(session.CanAffordAnyBet, Is.True);
            Assert.That(session.CanSpin, Is.False);

            bool started = session.TryBeginSpin(
                out SpinResult rejectedResult,
                out string error);

            Assert.That(started, Is.False);
            Assert.That(rejectedResult, Is.Null);
            Assert.That(error, Is.Not.Empty);
            Assert.That(session.Credits, Is.EqualTo(20));
            Assert.That(session.PendingResult, Is.Null);
            Assert.That(random.Calls, Is.Zero);

            Assert.That(session.TrySelectBet(0), Is.True);
            Assert.That(session.CanSpin, Is.True);

            Assert.That(session.TryBeginSpin(out _, out _), Is.True);
            Assert.That(session.TryCompleteSpin(), Is.True);
            Assert.That(session.Credits, Is.EqualTo(10));
            Assert.That(session.CanAffordAnyBet, Is.True);
            Assert.That(session.LatestResult, Is.Not.Null);

            Assert.That(session.TryBeginSpin(out _, out _), Is.True);
            Assert.That(session.LatestResult, Is.Null);
            Assert.That(session.TryCompleteSpin(), Is.True);

            Assert.That(session.Credits, Is.Zero);
            Assert.That(session.CanAffordAnyBet, Is.False);
            Assert.That(session.CanSpin, Is.False);
            Assert.That(random.Calls, Is.EqualTo(6));
        }

        [Test]
        public void Reset_RestoresCreditsLowestBetAndClearsResults()
        {
            SetSessionSettings(1000, 50, 10, 25);

            SequenceRandom random = new SequenceRandom(0d, 0.25d, 0.5d);
            SlotSession session = new SlotSession(config, random);

            Assert.That(session.SelectedBetIndex, Is.EqualTo(1));
            Assert.That(session.SelectedBet, Is.EqualTo(10));

            Assert.That(session.TrySelectBet(0), Is.True);
            Assert.That(session.TryBeginSpin(out _, out _), Is.True);
            Assert.That(session.TryCompleteSpin(), Is.True);
            Assert.That(session.Credits, Is.EqualTo(950));
            Assert.That(session.LatestResult, Is.Not.Null);

            Assert.That(session.TryReset(), Is.True);

            Assert.That(session.Credits, Is.EqualTo(1000));
            Assert.That(session.SelectedBetIndex, Is.EqualTo(1));
            Assert.That(session.SelectedBet, Is.EqualTo(10));
            Assert.That(session.PendingResult, Is.Null);
            Assert.That(session.LatestResult, Is.Null);
            Assert.That(session.IsSpinning, Is.False);
            Assert.That(session.CanSpin, Is.True);
            Assert.That(random.Calls, Is.EqualTo(3));
        }

        [Test]
        public void Spin_RejectsPayoutOverflowBeforeDrawing()
        {
            SetSessionSettings(int.MaxValue, 10, 25, 50);

            SequenceRandom random = new SequenceRandom(0d, 0.25d, 0.5d);
            SlotSession session = new SlotSession(config, random);

            Assert.That(session.CanSpin, Is.False);

            bool started = session.TryBeginSpin(
                out SpinResult result,
                out string error);

            Assert.That(started, Is.False);
            Assert.That(result, Is.Null);
            Assert.That(error, Is.Not.Empty);
            Assert.That(session.Credits, Is.EqualTo(int.MaxValue));
            Assert.That(session.PendingResult, Is.Null);
            Assert.That(session.IsSpinning, Is.False);
            Assert.That(random.Calls, Is.Zero);
        }

        #endregion

        #region Test Helpers

        private void SetSessionSettings(int startingCredits, params int[] bets)
        {
            using (SerializedObject serialized = new SerializedObject(config))
            {
                serialized.FindProperty("startingCredits").intValue =
                    startingCredits;

                SerializedProperty betPresets =
                    serialized.FindProperty("betPresets");

                betPresets.arraySize = bets.Length;

                for (int i = 0; i < bets.Length; i++)
                {
                    betPresets.GetArrayElementAtIndex(i).intValue = bets[i];
                }

                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

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
                        "The session requested more RNG samples than expected.");
                }

                return samples[nextIndex++];
            }
        }

        #endregion
    }
}