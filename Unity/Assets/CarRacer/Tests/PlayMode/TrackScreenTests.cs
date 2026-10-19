using System.Collections;
using CarRacer.Core.Driving;
using CarRacer.Game;
using CarRacer.Game.Driving;
using CarRacer.Game.Screens;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CarRacer.Tests
{
    /// <summary>
    /// Drives the real Unity car (rigidbody, colliders, generated track) to check the glue between
    /// <see cref="CarModel"/> and the physics engine. The handling itself is tested by `dotnet test`.
    /// </summary>
    public class TrackScreenTests
    {
        private sealed class ScriptedInput : ICarInputSource
        {
            public CarInput Input;

            public CarInput Read() => Input;
        }

        private ScriptedInput _input;
        private TrackScreen _screen;

        private CarController Car => _screen.Car;

        [SetUp]
        public void SetUp()
        {
            _input = new ScriptedInput();
            _screen = TrackScreen.Create(_input);
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(_screen.gameObject);
        }

        [UnityTest]
        public IEnumerator TheCarSettlesOnTheRoad()
        {
            yield return new WaitForSeconds(1f);
            Assert.IsTrue(Car.Grounded);
            Assert.That(Car.transform.position.y, Is.InRange(0f, 1.5f));
            Assert.That(Car.ForwardSpeed, Is.EqualTo(0f).Within(0.3f));
        }

        [UnityTest]
        public IEnumerator AWreckedCarStopsThenDrivesAgainOnceMended()
        {
            yield return new WaitForSeconds(0.5f);
            _input.Input = new CarInput(0f, 1f);
            yield return new WaitForSeconds(1f);
            Assert.That(Car.ForwardSpeed, Is.GreaterThan(8f));

            // Practice takes damage; four item hits wreck the car.
            Assert.IsTrue(Car.DamageEnabled);
            for (int i = 0; i < 4; i++)
            {
                Car.TakeItemHit();
            }

            Assert.IsTrue(Car.Damage.Wrecked);
            yield return new WaitForSeconds(1.5f);
            Assert.That(Car.ForwardSpeed, Is.LessThan(3f), "a wreck takes no throttle and slides to a stop");

            float giveUp = Time.time + CarDamage.WreckSeconds;
            while (Car.Damage.Wrecked && Time.time < giveUp)
            {
                yield return null;
            }

            Assert.IsFalse(Car.Damage.Wrecked);
            Assert.That(Car.Damage.Value, Is.EqualTo(0f));
            yield return new WaitForSeconds(0.5f);
            Assert.That(Car.ForwardSpeed, Is.GreaterThan(3f), "mended, it pulls away again");
        }

        [UnityTest]
        public IEnumerator FullThrottleDrivesDownTheStraight()
        {
            yield return new WaitForSeconds(0.5f);
            Vector3 start = Car.transform.position;
            Vector3 forward = Car.State.Forward.ToUnity();

            _input.Input = new CarInput(0f, 1f);
            yield return new WaitForSeconds(2f);

            // 16 m/s² for 2 s covers about 32 m with nothing in the way; allow for frame timing.
            float travelled = Vector3.Dot(Car.transform.position - start, forward);
            Assert.That(travelled, Is.GreaterThan(20f));
            Assert.That(Car.ForwardSpeed, Is.GreaterThan(20f));
            Assert.IsTrue(Car.Grounded);
        }

        [UnityTest]
        public IEnumerator BrakingBringsTheCarToAStop()
        {
            _input.Input = new CarInput(0f, 1f);
            yield return new WaitForSeconds(1.5f);
            Assert.That(Car.ForwardSpeed, Is.GreaterThan(10f));

            // Hold the brake until the car stops (holding it longer would start reversing).
            float startSpeed = Car.ForwardSpeed;
            float startTime = Time.fixedTime;
            _input.Input = new CarInput(0f, -1f);
            while (Car.ForwardSpeed > 0.5f && Time.fixedTime - startTime < 3f)
            {
                yield return new WaitForFixedUpdate();
            }

            _input.Input = default;
            float expected = startSpeed / Car.Tuning.Braking;
            Assert.That(Time.fixedTime - startTime, Is.LessThan(expected + 0.2f), $"braking from {startSpeed:0.0} m/s");

            yield return new WaitForSeconds(0.3f);
            Assert.That(Car.ForwardSpeed, Is.EqualTo(0f).Within(0.5f));
        }

        [UnityTest]
        public IEnumerator SteeringRightTurnsClockwise()
        {
            _input.Input = new CarInput(0f, 1f);
            yield return new WaitForSeconds(0.8f);
            float heading = Car.State.Heading;

            _input.Input = new CarInput(1f, 1f);
            yield return new WaitForSeconds(0.3f);
            Assert.That(Mathf.DeltaAngle(heading * Mathf.Rad2Deg, Car.State.Heading * Mathf.Rad2Deg), Is.GreaterThan(10f));
        }
    }
}
