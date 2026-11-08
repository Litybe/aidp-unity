using System;
using System.Collections.Generic;
using System.Reflection;

namespace Aidp.Playtest
{
    /// <summary>Registre des sondes et commandes declarees par attribut. Scanne les assemblies qui referencent le package.</summary>
    public sealed class PlaytestRegistry : IProbeSource
    {
        private readonly Dictionary<string, Type> _probeTypes = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Type> _commandTypes = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, IPlaytestProbe> _probes = new Dictionary<string, IPlaytestProbe>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, IPlaytestCommand> _commands = new Dictionary<string, IPlaytestCommand>(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _problems = new List<string>();

        public PlaytestRegistry(IEnumerable<Type> candidateTypes)
        {
            foreach (Type type in candidateTypes)
                Register(type);
        }

        /// <summary>Erreurs de declaration (doublon, interface manquante, constructeur absent).</summary>
        public IReadOnlyList<string> Problems => _problems;

        public IEnumerable<string> ProbeNames => _probeTypes.Keys;
        public IEnumerable<string> CommandNames => _commandTypes.Keys;

        public static PlaytestRegistry FromLoadedAssemblies()
        {
            string runtimeName = typeof(PlaytestRegistry).Assembly.GetName().Name;
            var types = new List<Type>();
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.IsDynamic || !ReferencesRuntime(assembly, runtimeName))
                    continue;
                types.AddRange(SafeTypes(assembly));
            }

            return new PlaytestRegistry(types);
        }

        public bool TryRead(string probe, string field, out object value, out string error)
        {
            value = null;
            if (!_probeTypes.ContainsKey(probe))
            {
                error = "unknown probe '" + probe + "' (known: " + string.Join(", ", _probeTypes.Keys) + ")";
                return false;
            }

            try
            {
                IPlaytestProbe instance = GetProbe(probe);
                if (instance is IPlaytestFieldProbe fieldProbe)
                    return fieldProbe.TryReadField(field, out value, out error);
                IReadOnlyDictionary<string, object> snapshot = instance.Read();
                if (snapshot == null)
                {
                    error = "probe '" + probe + "' has no subject (not in scene yet?)";
                    return false;
                }

                if (!snapshot.TryGetValue(field, out value))
                {
                    error = "probe '" + probe + "' has no field '" + field + "' (fields: " + string.Join(", ", snapshot.Keys) + ")";
                    return false;
                }

                error = null;
                return true;
            }
            catch (Exception e)
            {
                error = "probe '" + probe + "' threw " + e.GetType().Name + ": " + e.Message;
                return false;
            }
        }

        public void Execute(ScenarioCommand command)
        {
            if (!_commandTypes.ContainsKey(command.Name))
                throw new PlaytestException("unknown command '" + command.Name + "' (known: " + string.Join(", ", _commandTypes.Keys) + ")");
            if (!_commands.TryGetValue(command.Name, out IPlaytestCommand instance))
            {
                instance = (IPlaytestCommand)Activator.CreateInstance(_commandTypes[command.Name]);
                _commands[command.Name] = instance;
            }

            instance.Execute(command.Args);
        }

        private IPlaytestProbe GetProbe(string name)
        {
            if (!_probes.TryGetValue(name, out IPlaytestProbe instance))
            {
                instance = (IPlaytestProbe)Activator.CreateInstance(_probeTypes[name]);
                _probes[name] = instance;
            }

            return instance;
        }

        private void Register(Type type)
        {
            if (type == null || type.IsAbstract || type.IsInterface)
                return;
            var probe = (PlaytestProbeAttribute)Attribute.GetCustomAttribute(type, typeof(PlaytestProbeAttribute), false);
            if (probe != null)
                Add(_probeTypes, probe.Name, type, typeof(IPlaytestProbe), "probe");
            var command = (PlaytestCommandAttribute)Attribute.GetCustomAttribute(type, typeof(PlaytestCommandAttribute), false);
            if (command != null)
                Add(_commandTypes, command.Name, type, typeof(IPlaytestCommand), "command");
        }

        private void Add(Dictionary<string, Type> map, string name, Type type, Type contract, string kind)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                _problems.Add(kind + " " + type.FullName + " has an empty name");
                return;
            }

            if (!contract.IsAssignableFrom(type))
            {
                _problems.Add(kind + " " + type.FullName + " does not implement " + contract.Name);
                return;
            }

            if (type.GetConstructor(Type.EmptyTypes) == null)
            {
                _problems.Add(kind + " " + type.FullName + " needs a public parameterless constructor");
                return;
            }

            if (map.TryGetValue(name, out Type existing))
            {
                _problems.Add(kind + " '" + name + "' declared twice (" + existing.FullName + ", " + type.FullName + ")");
                return;
            }

            map[name] = type;
        }

        private static bool ReferencesRuntime(Assembly assembly, string runtimeName)
        {
            // Le runtime lui-meme declare la commande "spawn" et la sonde "object".
            if (assembly.GetName().Name == runtimeName)
                return true;
            try
            {
                foreach (AssemblyName reference in assembly.GetReferencedAssemblies())
                    if (reference.Name == runtimeName)
                        return true;
            }
            catch (Exception)
            {
                return false;
            }

            return false;
        }

        private static IEnumerable<Type> SafeTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                var loaded = new List<Type>();
                foreach (Type t in e.Types)
                    if (t != null)
                        loaded.Add(t);
                return loaded;
            }
        }
    }
}
