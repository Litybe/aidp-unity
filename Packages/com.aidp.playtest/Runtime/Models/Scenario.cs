using System.Collections.Generic;

namespace Aidp.Playtest
{
    public enum StepKind
    {
        Hold,
        Press,
        Release,
        Tap,
        Wait,
        Capture,
        Command
    }

    public sealed class ScenarioCommand
    {
        public ScenarioCommand(string name, PlaytestArgs args)
        {
            Name = name;
            Args = args;
        }

        public string Name { get; }
        public PlaytestArgs Args { get; }
    }

    public sealed class ScenarioStep
    {
        public ScenarioStep(StepKind kind, IReadOnlyList<string> keys, int ms, string name, ScenarioCommand command)
        {
            Kind = kind;
            Keys = keys;
            Ms = ms;
            Name = name;
            Command = command;
        }

        public StepKind Kind { get; }

        /// <summary>Touches (hold, press, release, tap).</summary>
        public IReadOnlyList<string> Keys { get; }

        /// <summary>Duree en ms (hold, wait).</summary>
        public int Ms { get; }

        /// <summary>Nom de la capture.</summary>
        public string Name { get; }

        public ScenarioCommand Command { get; }
    }

    public sealed class ScenarioAssertion
    {
        public ScenarioAssertion(AssertionExpression expression, int withinMs)
        {
            Expression = expression;
            WithinMs = withinMs;
        }

        public AssertionExpression Expression { get; }

        /// <summary>0 = evaluee une fois, a la fin des steps ; sinon vraie au moins une fois dans ce delai.</summary>
        public int WithinMs { get; }
    }

    /// <summary>Un scenario de playtest (schema C2).</summary>
    public sealed class Scenario
    {
        public Scenario(string id, string scene, int timeoutMs, IReadOnlyList<ScenarioCommand> setup,
            IReadOnlyList<ScenarioStep> steps, IReadOnlyList<ScenarioAssertion> assertions)
        {
            Id = id;
            Scene = scene;
            TimeoutMs = timeoutMs;
            Setup = setup;
            Steps = steps;
            Assertions = assertions;
        }

        public string Id { get; }

        /// <summary>Chemin de scene (Assets/...unity), vide = scene courante.</summary>
        public string Scene { get; }

        public int TimeoutMs { get; }
        public IReadOnlyList<ScenarioCommand> Setup { get; }
        public IReadOnlyList<ScenarioStep> Steps { get; }
        public IReadOnlyList<ScenarioAssertion> Assertions { get; }
    }
}
