using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace DI
{
    public class DIContainer : IDisposable
    {
        // Родительский контейнер
        private readonly DIContainer _parentContainer;

        // Текущие сущности в данном контейнере
        private readonly Dictionary<(string, Type), DIEntry> _entriesMap = new Dictionary<(string, Type), DIEntry>();

        // Список для проверки (без автоподставления) на циклическую зависимость
        private readonly HashSet<(string, Type)> _resolutionStack = new HashSet<(string, Type)>();

        // Список для проверки (с автоподставлением) на циклическую зависимость
        private readonly HashSet<Type> _typeResolutionStack = new HashSet<Type>();

        public DIContainer(DIContainer parentContainer = null)
        {
            _parentContainer = parentContainer;
        }

        /// <summary>
        /// Регистрация еще не созданного объекта без тега.
        /// </summary>
        /// <typeparam name="T">Тип объекта.</typeparam>
        /// <param name="factory">Параметры, необходимые для объекта.</param>
        /// <returns>Сущность в контейнере.</returns>
        public DIEntry RegisterFactory<T>(Func<DIContainer, T> factory)
        {
            return RegisterFactory(null, factory);
        }

        /// <summary>
        /// Регистрация еще не созданного объекта с тегом.
        /// </summary>
        /// <typeparam name="T">Тип объекта.</typeparam>
        /// <param name="tag">Тег объекта.</param>
        /// <param name="factory">Параметры, необходимые для объекта.</param>
        /// <returns>Сущность в контейнере.</returns>
        /// <exception cref="Exception">Ошибка, означающая, что объект уже зарегистрирован.</exception>
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

        /// <summary>
        /// Регистрация ссылки на созданный объект без тега.
        /// </summary>
        /// <typeparam name="T">Тип объекта.</typeparam>
        /// <param name="instance">Ссылка на объект.</param>
        public void RegisterInstance<T>(T instance)
        {
            RegisterInstance(null, instance);
        }

        /// <summary>
        /// Регистрация ссылки на созданный объект с тегом.
        /// </summary>
        /// <typeparam name="T">Тип объекта.</typeparam>
        /// <param name="tag">Тег объекта.</param>
        /// <param name="instance">Ссылка на объект.</param>
        /// <exception cref="Exception">Ошибка, означающая, что объект уже зарегистрирован.</exception>
        public void RegisterInstance<T>(string tag, T instance)
        {
            var key = (tag, typeof(T));

            if (_entriesMap.ContainsKey(key))
            {
                throw new Exception(
                    $"DI: Instance with tag {key.Item1} and type {key.Item2.FullName} has already registered");
            }

            var diEntry = new DIEntry<T>(this, instance); // передаём контейнер
            _entriesMap[key] = diEntry;
        }

        /// <summary>
        /// Получение объекта из контейнера.
        /// </summary>
        /// <typeparam name="T">Тип объекта.</typeparam>
        /// <param name="tag">Тег объекта.</param>
        /// <returns>Объект из контейнера.</returns>
        /// <exception cref="InvalidOperationException">Ошибка, означающая, что имеются циклические зависимости.</exception>
        /// <exception cref="Exception">Ошибка, означающая, что для объекта не найдена зависимость.</exception>
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

        /// <summary>
        /// Специальная регистрация еще не созданного объекта.
        /// </summary>
        /// <typeparam name="T">Тип объекта.</typeparam>
        /// <returns>Сущность в контейнере.</returns>
        /// <remarks>Это специальная регистрация с автоподстановкой данных.</remarks>
        public DIEntry RegisterType<T>() where T : class
        {
            return RegisterFactory(c => c.ResolveWithConstructorInjection<T>()).AsSingle();
        }

        /// <summary>
        /// Специальное получение объекта из контейнера.
        /// </summary>
        /// <typeparam name="T">Тип обхекта.</typeparam>
        /// <returns>Объект из контейнера.</returns>
        /// <remarks>Возвращает объект, для которого все зависимые объекты будут созданы автоматически.
        /// !!!Тег не учитывается!!!</remarks>
        public T ResolveWithConstructorInjection<T>() where T : class
        {
            return (T)ResolveByType(typeof(T));
        }

        /// <summary>
        /// Получение объекта с автоподставкой аргументов.
        /// </summary>
        /// <param name="type">Тип объекта.</param>
        /// <returns>Объект из контейнера.</returns>
        /// <exception cref="InvalidOperationException">Ошибка, означающая, что имеются циклические зависимости.</exception>
        /// <exception cref="InvalidOperationException">Ошибка, означающая, что нет подходящих конструкторов.</exception>
        /// <remarks>!!!Тег не учитывается!!!</remarks>
        private object ResolveByType(Type type)
        {
            if (_typeResolutionStack.Contains(type))
                throw new InvalidOperationException(
                    $"DI: Detected cyclic dependency while constructing type '{type.FullName}'.");

            _typeResolutionStack.Add(type);

            try
            {
                var key = ((string)null, type);

                if (_entriesMap.TryGetValue(key, out var entry))
                    return entry.ResolveObject();

                var constructors = type.GetConstructors()
                    .OrderByDescending(c => c.GetParameters().Length)
                    .ToArray();
                ConstructorInfo selectedCtor = null;

                foreach (var ctor in constructors)
                {
                    var parameters = ctor.GetParameters();
                    bool canBuild = true;

                    foreach (var param in parameters)
                    {
                        if (IsPrimitiveOrBuiltIn(param.ParameterType))
                            continue;

                        if (!CanResolveType(param.ParameterType))
                        {
                            canBuild = false;
                            break;
                        }
                    }

                    if (canBuild)
                    {
                        selectedCtor = ctor;
                        break;
                    }
                }

                if (selectedCtor == null)
                {
                    throw new InvalidOperationException(
                        $"DI: No suitable constructor found for '{type.FullName}'. " +
                        "Register all dependencies or add a constructor with primitive-only parameters.");
                }

                var args = new object[selectedCtor.GetParameters().Length];
                var ctorParams = selectedCtor.GetParameters();

                for (int i = 0; i < ctorParams.Length; i++)
                {
                    args[i] = ResolveByType(ctorParams[i].ParameterType);
                }

                return selectedCtor.Invoke(args);
            }
            catch
            {
                _typeResolutionStack.Remove(type);
                throw;
            }
        }

        /// <summary>
        /// Проверка на возможность создания объекта.
        /// </summary>
        /// <param name="type">Тип объекта.</param>
        /// <returns>Результат проверки.</returns>
        /// <remarks>!!!Тег не учитывается!!!</remarks>
        private bool CanResolveType(Type type)
        {
            var key = ((string)null, type);
            if (_entriesMap.ContainsKey(key))
                return true;
            if (_parentContainer != null)
                return _parentContainer.CanResolveType(type);
            return false;
        }

        /// <summary>
        /// Проверка на необходимость создания объекта.
        /// </summary>
        /// <param name="type">Тип объекта.</param>
        /// <returns>Результат проверки.</returns>
        /// <remarks>Не считает примитивные типы сервисом.</remarks>
        private bool IsPrimitiveOrBuiltIn(Type type)
        {
            return type.IsPrimitive ||
                   type == typeof(string) ||
                   type == typeof(decimal) ||
                   type == typeof(DateTime) ||
                   type == typeof(Guid);
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
