using System;
using System.Collections.Generic;
using System.Linq;

namespace DI
{
    public class DIContainer : IDisposable
    {
        private readonly DIContainer _parentContainer;

        private readonly Dictionary<(string, Type), DIEntry> _entriesMap = new Dictionary<(string, Type), DIEntry>();

        private readonly HashSet<(string, Type)> _resolutionStack = new HashSet<(string, Type)>();

        private readonly HashSet<Type> _typeResolutionStack = new HashSet<Type>();

        public DIContainer(DIContainer parentContainer = null)
        {
            _parentContainer = parentContainer;
        }

        public DIEntry RegisterFactory<T>(Func<DIContainer, T> factory)
        {
            return RegisterFactory(null, factory);
        }

        public DIEntry RegisterFactory<T>(string tag, Func<DIContainer, T> factory)
        {
            var key = (tag, typeof(T));

            if (_entriesMap.ContainsKey(key))
            {
                throw new Exception(
                    $"DI: Factory with tag {key.Item1} and type {key.Item2.FullName} has already registered");
            }

            var diEntry = new DIEntry<T>(this, factory);
            _entriesMap[key] = diEntry;
            return diEntry;
        }

        public void RegisterInstance<T>(T instance)
        {
            RegisterInstance(null, instance);
        }

        public void RegisterInstance<T>(string tag, T instance)
        {
            var key = (tag, typeof(T));

            if (_entriesMap.ContainsKey(key))
            {
                throw new Exception(
                    $"DI: Instance with tag {key.Item1} and type {key.Item2.FullName} has already registered");
            }

            var diEntry = new DIEntry<T>(instance);
            _entriesMap[key] = diEntry;
        }

        public T Resolve<T>(string tag = null)
        {
            var key = (tag, typeof(T));

            if (_resolutionStack.Contains(key))
            {
                throw new InvalidOperationException(
                    $"DI: Detected cyclic dependency for tag '{key.Item1}' and type '{typeof(T).FullName}'. " +
                    "Check your dependency graph.");
            }

            _resolutionStack.Add(key);

            try
            {
                if (_entriesMap.TryGetValue(key, out var diEntry))
                {
                    return diEntry.Resolve<T>();
                }

                if (_parentContainer != null)
                {
                    return _parentContainer.Resolve<T>(tag);
                }
            }
            finally
            {
                _resolutionStack.Remove(key);
            }

            throw new Exception($"Couldn't find dependency for tag {tag} and type {typeof(T).FullName}");
        }

        private object ResolveByType(Type type)
        {
            if (_typeResolutionStack.Contains(type))
            {
                throw new InvalidOperationException(
                    $"DI: Detected cyclic dependency while constructing type '{type.FullName}'. " +
                    "A depends on B, B depends on A. Refactor your dependencies.");
            }

            _typeResolutionStack.Add(type);

            try
            {
                var key = ((string)null, type);

                if (_entriesMap.TryGetValue(key, out var entry))
                {
                    return entry.ResolveObject();
                }

                var ctor = type.GetConstructors()
                    .OrderByDescending(c => c.GetParameters().Length)
                    .FirstOrDefault();

                if (ctor == null)
                    throw new Exception($"No public constructor found for {type.FullName}");

                var parameters = ctor.GetParameters();
                var args = new object[parameters.Length];

                for (int i = 0; i < parameters.Length; i++)
                {
                    args[i] = ResolveByType(parameters[i].ParameterType);
                }

                return ctor.Invoke(args);
            }
            catch
            {
                _typeResolutionStack.Remove(type);
                throw;
            }
        }

        public T ResolveWithConstructorInjection<T>() where T : class
        {
            return (T)ResolveByType(typeof(T));
        }

        public DIEntry RegisterType<T>() where T : class
        {
            return RegisterFactory(c => c.ResolveWithConstructorInjection<T>());
        }

        public void Dispose()
        {
            foreach (var entry in _entriesMap.Values)
            {
                entry.Dispose();
            }
        }
    }
}
