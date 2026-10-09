using System;

namespace DI
{
    public abstract class DIEntry : IDisposable
    {
        // Контейнер сущности.
        protected DIContainer Container { get; }
        // Признак синглтона.
        protected bool IsSingleton { get; set; }

        protected DIEntry() { }

        protected DIEntry(DIContainer container)
        {
            Container = container;
        }

        /// <summary>
        /// Получение объекта из контейнера.
        /// </summary>
        /// <typeparam name="T">Тип объкта.</typeparam>
        public T Resolve<T>()
        {
            return ((DIEntry<T>)this).Resolve();
        }

        /// <summary>
        /// Специальное получение объекта (с автоподставкой).
        /// </summary>
        public abstract object ResolveObject();

        /// <summary>
        /// Установка признака синглтона.
        /// </summary>
        public DIEntry AsSingle()
        {
            IsSingleton = true;

            return this;
        }

        public abstract void Dispose();
    }

    public class DIEntry<T> : DIEntry
    {
        private Func<DIContainer, T> _factory { get; }
        private T _instance;

        public DIEntry(DIContainer container, Func<DIContainer, T> factory) : base(container)
        {
            _factory = factory;
        }

        public DIEntry(DIContainer container, T value) : base(container)
        {
            _instance = value;
            IsSingleton = true;
        }

        public T Resolve()
        {
            if (IsSingleton)
            {
                if (_instance == null)
                {
                    _instance = _factory(Container);
                }

                return _instance;
            }

            return _factory(Container);
        }

        public override object ResolveObject() => Resolve();

        /// <summary>
        /// Освобождение ресурсов.
        /// </summary>
        /// <remarks>Освобождает только синглтоны и объекты реализующие IDisposable.</remarks>
        public override void Dispose()
        {
            if (IsSingleton && _instance is IDisposable disposable)
            {
                disposable.Dispose();
            }

            _instance = default;
        }
    }
}
