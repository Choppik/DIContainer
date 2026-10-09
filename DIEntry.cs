using System;

namespace DI
{
    public abstract class DIEntry : IDisposable
    {
        protected DIContainer Container { get; }
        protected bool IsSingleton { get; set; }

        protected DIEntry() { }

        protected DIEntry(DIContainer container)
        {
            Container = container;
        }

        public T Resolve<T>()
        {
            return ((DIEntry<T>)this).Resolve();
        }

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
        private IDisposable _disposableInstance;

        public DIEntry(DIContainer container, Func<DIContainer, T> factory) : base(container)
        {
            _factory = factory;
        }

        public DIEntry(T value)
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
